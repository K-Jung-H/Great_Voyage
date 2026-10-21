using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class PestRules
    {
        private readonly GameRuntime game;
        private readonly EventSettings settings;
        public PestRules(GameRuntime game, EventSettings settings) { this.game=game;this.settings=settings; }
        public PestState[] Active => game.State.events.current?.pests?.Where(value=>value.health>0).ToArray() ?? Array.Empty<PestState>();
        public PestState Find(string id) => Active.FirstOrDefault(value=>value.id==id);
        public void Begin()
        {
            var current=game.State.events.current;
            if(current.pests!=null)return;
            var target=game.Ship.Facilities.Where(value=>value.durability>0).OrderBy(value=>value.id,StringComparer.Ordinal).FirstOrDefault();
            current.pests=new System.Collections.Generic.List<PestState>();
            if(target==null)return;
            for(int i=0;i<2;i++)current.pests.Add(new PestState{id=current.id+":pest:"+i,floorId=target.floorId,targetId=target.id,position=game.Ship.EntryPosition(target.floorId)});
        }
        public void Advance(float seconds)
        {
            foreach(var pest in Active)
            {
                var target=game.Ship.GetFacility(pest.targetId);
                if(target==null||target.durability<=0)
                {
                    target=game.Ship.Facilities.Where(value=>value.floorId==pest.floorId&&value.durability>0).OrderBy(value=>value.id,StringComparer.Ordinal).FirstOrDefault();
                    pest.targetId=target?.id;
                }
                if(target==null||!game.Ship.TryGetAccessCell(target.id,out var access))continue;
                if(Vector2.Distance(pest.position,access)<=.25f*game.Ship.CellSize)target.durability=Mathf.Max(0,target.durability-settings.pestDamage*seconds);
                else if(game.Ship.TryFindPath(pest.floorId,pest.position,access,out var path)&&path.Count>0)
                {
                    var next=path.FirstOrDefault(position=>Vector2.Distance(position,pest.position)>.05f*game.Ship.CellSize);
                    pest.position=Vector2.MoveTowards(pest.position,next,.8f*game.Ship.CellSize*seconds);
                }
            }
        }
        public void Strike(string id,float damage)
        {
            var target=Find(id);if(target!=null)target.health=Mathf.Max(0,target.health-damage);
        }
    }
}
