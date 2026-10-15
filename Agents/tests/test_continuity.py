import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('continuity', Path(__file__).parents[1] / 'continuity.py')
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)

class ContinuityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.j = c.Journal(self.root)
        (self.root / 'Agents/config.json').write_text('{"queue_events": 2}', encoding='utf-8')

    def test_fifo_and_view_recovery(self):
        ids = [self.j.add('message', {'text': f'한글 {i}\n```'}) for i in range(5)]
        (self.root / 'Agents/Queue.md').write_text('broken', encoding='utf-8')
        self.j.render(self.j.events())
        queue = (self.root / 'Agents/Queue.md').read_text(encoding='utf-8')
        history = (self.root / 'Agents/History.md').read_text(encoding='utf-8')
        for i, event_id in enumerate(ids):
            self.assertEqual(event_id in queue, i >= 3)
            self.assertEqual(event_id in history, i < 3)

    def test_pending_survives_rotation_and_resolves(self):
        self.j.add('command.started', {'argv': ['example']}, 'op')
        for i in range(4):
            self.j.add('message', {'text': str(i)})
        state = self.root / 'Agents/State.md'
        self.assertIn('`op`', state.read_text(encoding='utf-8'))
        self.j.add('command.resolved', {'evidence': 'inspected actual state'}, 'op')
        self.assertNotIn('`op`', state.read_text(encoding='utf-8'))

    def test_failed_command_and_original_output(self):
        code = c.execute(self.j, [sys.executable, '-c', 'import sys; print("output"); print("error", file=sys.stderr); sys.exit(7)'])
        self.assertEqual(code, 7)
        events = self.j.events()
        self.assertEqual(events[0]['kind'], 'command.started')
        self.assertEqual(events[-1]['data']['exit_code'], 7)
        self.assertEqual({e['data']['text'] for e in events if e['kind'] == 'command.output'}, {'output\n', 'error\n'})

    def test_codex_stream_and_reasoning_exclusion(self):
        fixture = self.root / 'fake.py'
        fixture.write_text('import sys,json\nsys.stdin.read()\nfor e in ' + repr([
            {'type': 'item.started', 'item': {'id': 'a', 'type': 'command_execution', 'command': 'example'}},
            {'type': 'item.completed', 'item': {'id': 'b', 'type': 'reasoning', 'text': 'excluded'}},
            {'type': 'item.completed', 'item': {'id': 'c', 'type': 'agent_message', 'text': 'answer'}}
        ]) + ': print(json.dumps(e), flush=True)\n', encoding='utf-8')
        self.assertEqual(c.execute(self.j, [sys.executable, str(fixture)], 'request'), 0)
        self.assertNotIn('excluded', json.dumps(self.j.events()))
        self.assertIn(':a`', (self.root / 'Agents/State.md').read_text(encoding='utf-8'))

    def test_lock_and_corruption_fail_closed(self):
        lock = self.root / 'Agents/.continuity-lock'
        lock.mkdir()
        with self.assertRaises(RuntimeError):
            self.j.add('message', {})
        lock.rmdir()
        (self.root / 'Agents/events.json').write_text('broken', encoding='utf-8')
        with self.assertRaises(json.JSONDecodeError):
            self.j.add('message', {})

if __name__ == '__main__':
    unittest.main()
