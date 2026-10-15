"""Portable continuity journal. Python 3.10+, standard library only."""
import argparse
import contextlib
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import uuid

ROOT = Path(__file__).resolve().parent.parent

def atomic(path, text):
    temp = path.with_name(path.name + '.tmp')
    with temp.open('w', encoding='utf-8', newline='\n') as f:
        f.write(text)
        f.flush()
        os.fsync(f.fileno())
    os.replace(temp, path)

class Journal:
    def __init__(self, root):
        self.root = Path(root)
        self.folder = self.root / 'Agents'
        self.folder.mkdir(exist_ok=True)
        self.mutex = threading.RLock()

    @contextlib.contextmanager
    def lock(self):
        with self.mutex:
            lock = self.folder / '.continuity-lock'
            try:
                lock.mkdir()
            except FileExistsError:
                raise RuntimeError('Journal locked. Check active writers before removing Agents/.continuity-lock.')
            try:
                yield
            finally:
                lock.rmdir()

    def events(self):
        path = self.folder / 'events.json'
        return json.loads(path.read_text(encoding='utf-8')) if path.exists() else []

    def add(self, kind, data, operation=None):
        with self.lock():
            events = self.events()
            event = dict(id=str(uuid.uuid4()), time=dt.datetime.now(dt.timezone.utc).isoformat(),
                         kind=kind, operation=operation, data=data)
            events.append(event)
            atomic(self.folder / 'events.json', json.dumps(events, ensure_ascii=False, indent=2) + '\n')
            self.render(events)
            return event['id']

    def render(self, events):
        config = json.loads((self.folder / 'config.json').read_text(encoding='utf-8'))
        limit = config['queue_events']
        if type(limit) is not int or limit < 1:
            raise ValueError('queue_events must be a positive integer')
        def page(title, rows):
            text = '# ' + title + '\n\n자동 생성: events.json이 원본입니다. 직접 편집하지 마세요.\n\n'
            for e in rows:
                text += '## ' + e['time'] + ' | ' + e['kind'] + ' | ' + e['id'] + '\n\n'
                # Indentation prevents arbitrary payloads from closing Markdown fences.
                text += '\n'.join('    ' + line for line in json.dumps(e, ensure_ascii=False, indent=2).splitlines()) + '\n\n'
            return text
        atomic(self.folder / 'History.md', page('History', events[:-limit]))
        atomic(self.folder / 'Queue.md', page('Queue', events[-limit:]))
        pending = {}
        for e in events:
            op = e.get('operation')
            if e['kind'] == 'codex.event':
                item = e['data'].get('item', {})
                item_op = str(op) + ':' + str(item.get('id'))
                if e['data'].get('type') == 'item.started' and item.get('type') in ('command_execution', 'mcp_tool_call', 'file_change'):
                    pending[item_op] = e
                elif e['data'].get('type') == 'item.completed':
                    pending.pop(item_op, None)
            if op and e['kind'].endswith('.started'):
                pending[op] = e
            elif op and e['kind'].endswith(('.completed', '.resolved')):
                pending.pop(op, None)
        state = self.folder / 'State.md'
        marker = '<!-- continuity:generated -->'
        human = state.read_text(encoding='utf-8').split(marker)[0] if state.exists() else '# State\n\n'
        generated = '\n## 자동 기록 상태\n\n'
        generated += f'- 기록 이벤트: {len(events)}\n- 미완료 작업: {len(pending)}\n'
        for op, e in pending.items():
            generated += f'- `{op}`: {e["kind"]} — 결과 확인 전 재실행 금지\n'
        if events:
            generated += f'- 마지막 이벤트: {events[-1]["time"]} / {events[-1]["kind"]}\n'
        atomic(state, human.rstrip() + '\n\n' + marker + generated)

    def recover(self):
        with self.lock():
            self.render(self.events())
        parts = []
        for name in ['AGENTS.md', 'Agents/Rules.md', 'Agents/RoadMap.md', 'Agents/GameDesign.md', 'Agents/State.md', 'Agents/Queue.md']:
            parts.append(f'\n--- {name} ---\n' + (self.root / name).read_text(encoding='utf-8'))
        result = subprocess.run(['git', 'status', '--short', '--branch'], cwd=self.root, capture_output=True, text=True)
        parts.append('\n--- Actual Git status ---\n' + result.stdout + result.stderr)
        return '\n'.join(parts)

def execute(journal, command, prompt=None):
    op = str(uuid.uuid4())
    journal.add('command.started', {'argv': command, 'cwd': str(journal.root)}, op)
    try:
        process = subprocess.Popen(command, cwd=journal.root, stdin=subprocess.PIPE if prompt is not None else None,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8', errors='replace')
        def consume(pipe, stream):
            for line in pipe:
                if prompt is not None and stream == 'stdout':
                    try:
                        event = json.loads(line)
                    except ValueError:
                        journal.add('collector.unparsed', {'stream': stream, 'text': line}, op)
                        continue
                    if event.get('item', {}).get('type') in ('reasoning', 'reasoning_summary'):
                        continue
                    journal.add('codex.event', event, op)
                else:
                    journal.add('command.output', {'stream': stream, 'text': line}, op)
                print(line, end='', file=sys.stdout if stream == 'stdout' else sys.stderr)
        errors = []
        def guarded(pipe, stream):
            try:
                consume(pipe, stream)
            except Exception as exc:
                errors.append(exc)
                process.terminate()
        threads = [threading.Thread(target=guarded, args=(process.stdout, 'stdout')),
                   threading.Thread(target=guarded, args=(process.stderr, 'stderr'))]
        for thread in threads:
            thread.start()
        if prompt is not None:
            process.stdin.write(prompt)
            process.stdin.close()
        code = process.wait()
        for thread in threads:
            thread.join()
        if errors:
            raise errors[0]
        journal.add('command.completed', {'exit_code': code}, op)
        return code
    except BaseException as exc:
        if 'process' in locals() and process.poll() is None:
            process.terminate()
            process.wait()
        journal.add('command.interrupted', {'error': str(exc), 'check_actual_state_before_retry': True}, op)
        raise
    finally:
        if 'process' in locals():
            for thread in locals().get('threads', []):
                if thread.ident is not None:
                    thread.join()
            for pipe in (process.stdin, process.stdout, process.stderr):
                if pipe is not None:
                    pipe.close()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    sub.add_parser('recover')
    p = sub.add_parser('record'); p.add_argument('kind'); p.add_argument('file')
    p = sub.add_parser('resolve'); p.add_argument('operation'); p.add_argument('evidence_file')
    p = sub.add_parser('run'); p.add_argument('command', nargs=argparse.REMAINDER)
    p = sub.add_parser('session'); p.add_argument('prompt_file'); p.add_argument('--codex', default='codex')
    args = parser.parse_args()
    journal = Journal(ROOT)
    if args.action == 'recover':
        print(journal.recover())
    elif args.action == 'record':
        journal.add(args.kind, {'text': Path(args.file).read_text(encoding='utf-8')})
    elif args.action == 'resolve':
        journal.add('command.resolved', {'evidence': Path(args.evidence_file).read_text(encoding='utf-8')}, args.operation)
    elif args.action == 'run':
        command = args.command
        if command[:1] == ['--']:
            command = command[1:]
        if not command:
            parser.error('run requires a command')
        return execute(journal, command)
    else:
        context = journal.recover()
        prompt = Path(args.prompt_file).read_text(encoding='utf-8')
        journal.add('user.message', {'text': prompt})
        journal.add('session.started', {'recovery_loaded': True})
        result = execute(journal, [args.codex, 'exec', '--json', '--sandbox', 'workspace-write', '-'],
                         'Read the recovery context, compare actual files, and continue safely.\n' + context + '\nUSER REQUEST:\n' + prompt)
        journal.add('session.completed', {'exit_code': result})
        return result
    return 0

if __name__ == '__main__':
    sys.exit(main())
