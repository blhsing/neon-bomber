'use strict';
function forecastBombCell(b){
 if(b.airborne)return{x:b.airborne.toX,y:b.airborne.toY};let x=Math.floor(b.x),y=Math.floor(b.y);if(!b.moving)return{x,y};
 let steps=Math.max(0,Math.ceil(Math.max(0,b.fuse)*6));
 while(steps-->0){let nx=x+b.moving.x,ny=y+b.moving.y;if(nx<=0||ny<=0||nx>=COLS-1||ny>=ROWS-1||grid[ny]?.[nx]!==0||bombReservationAt(nx,ny,b))break;x=nx;y=ny}
 return{x,y};
}
function hypotheticalBomb(p,x,y,tag='假想爆彈'){
 return{tag,ref:null,x,y,blockX:x,blockY:y,range:p.range,fuse:p.remote?REMOTE_BOMB_FUSE:NORMAL_BOMB_FUSE,mega:p.mega>0,cluster:p.cluster>0,pierce:p.pierce,owner:p};
}
function blastShape(spec,simGrid){
 let main=[{x:spec.x,y:spec.y}],endpoints=[],destroyed=[];
 for(const [dx,dy] of AI_DIRS){let pierced=false;for(let i=1;i<=spec.range+(spec.mega?2:0);i++){
   let x=spec.x+dx*i,y=spec.y+dy*i,g=simGrid[y]?.[x];if(g==null||g===1)break;main.push({x,y});
   if(g===2){destroyed.push({x,y});if(!spec.pierce||pierced)break;pierced=true}endpoints.push({x,y});
 }}
 return{main,endpoints,destroyed};
}
function buildDangerForecast(horizon=4.5,extras=[],forcedTimes=null){
 let specs=bombs.filter(b=>!b.dead).map(b=>{let pos=forecastBombCell(b),landAt=b.airborne?Math.max(0,b.airborne.duration-b.airborne.elapsed):0;return{tag:null,ref:b,x:pos.x,y:pos.y,blockX:pos.x,blockY:pos.y,landAt,range:b.range,fuse:Math.max(0,b.fuse)+landAt,mega:b.mega,cluster:b.cluster,pierce:b.pierce,owner:b.owner}}).concat(extras);
 let times=specs.map(s=>forcedTimes?.has(s.ref)?forcedTimes.get(s.ref):Math.max(0,s.fuse)),finalShapes=specs.map(()=>({main:[],all:[],destroyed:[]}));
 for(let pass=0;pass<Math.max(2,specs.length+2);pass++){
   let changed=false,simGrid=grid.map(row=>row.slice()),order=specs.map((_,i)=>i).sort((a,b)=>times[a]-times[b]),shapes=specs.map(()=>({main:[],all:[],destroyed:[]}));
   for(const i of order){let shape=blastShape(specs[i],simGrid),mainSet=new Set(shape.main.map(c=>key(c.x,c.y)));
      for(let j=0;j<specs.length;j++)if(j!==i&&times[i]>=(specs[j].landAt||0)&&mainSet.has(key(specs[j].x,specs[j].y))&&times[j]>times[i]+.035){times[j]=times[i]+.035;changed=true}
     for(const c of shape.destroyed)if(simGrid[c.y]?.[c.x]===2)simGrid[c.y][c.x]=0;
     let all=shape.main.slice();if(specs[i].cluster&&shape.endpoints.length){let every=Math.max(1,Math.floor(shape.endpoints.length/6));shape.endpoints.forEach((e,n)=>{if(n%every)return;for(const [dx,dy] of [[1,1],[-1,1],[1,-1],[-1,-1]]){let x=e.x+dx,y=e.y+dy;if(simGrid[y]?.[x]===0)all.push({x,y})}})}
     shapes[i]={main:shape.main,all,destroyed:shape.destroyed};
   }
   finalShapes=shapes;if(!changed)break;
 }
 let windows=new Map(),put=(x,y,start,end,cause)=>{if(start>horizon||end<0)return;let k=key(x,y),a=windows.get(k)||[];a.push({start:Math.max(0,start),end:Math.min(horizon,end),cause});windows.set(k,a)};
 for(const f of flames)if(f.life>0)put(f.x,f.y,0,f.life,'現有火焰');
 specs.forEach((s,i)=>{if(times[i]<=horizon)for(const c of finalShapes[i].all)put(c.x,c.y,times[i],times[i]+(s.mega?.82:.76),s)});
 for(const list of windows.values())list.sort((a,b)=>a.start-b.start);
 const dangerDuring=(x,y,a,b,margin=0)=>{let list=windows.get(key(x,y));return!!list?.some(w=>b>=w.start-margin&&a<=w.end+margin)};
 const nextDanger=(x,y,after=0)=>{let list=windows.get(key(x,y));if(!list)return Infinity;for(const w of list){if(w.end>=after)return w.start<=after?0:w.start-after}return Infinity};
  const bombBlocked=(x,y,at,p)=>{if(p.bombpass)return false;return specs.some((s,i)=>at>=(s.landAt||0)&&at<times[i]-.025&&((s.blockX===x&&s.blockY===y)||(s.x===x&&s.y===y)))};
 return{horizon,specs,times,shapes:finalShapes,windows,dangerDuring,nextDanger,bombBlocked,maxEnd:Math.max(0,...[...windows.values()].flat().map(w=>w.end)),timeForTag:tag=>{let i=specs.findIndex(s=>s.tag===tag);return i<0?Infinity:times[i]},cellsForTag:tag=>{let i=specs.findIndex(s=>s.tag===tag);return i<0?[]:finalShapes[i].all},cellsForRef:ref=>{let i=specs.findIndex(s=>s.ref===ref);return i<0?[]:finalShapes[i].all}};
}
function aiDangerDuring(p,forecast,x,y,a,b,margin){return!p.flamepass&&forecast.dangerDuring(x,y,a,b,margin)}
function reconstructTimed(nodes,index){let route=[];while(index>=0&&nodes[index].parent>=0){route.push({x:nodes[index].x,y:nodes[index].y,t:nodes[index].t});index=nodes[index].parent}return route.reverse()}
function findSurvivalRoute(p,forecast,endTime,cfg,allowWait=true){
 let sx=Math.floor(p.x),sy=Math.floor(p.y);if(p.flamepass)return[];endTime=Math.min(forecast.horizon,Math.max(.2,endTime));
 let step=aiStepTime(p),maxSteps=Math.min(56,Math.ceil(endTime/step)+5),nodes=[{x:sx,y:sy,step:0,t:0,parent:-1}],queue=[0],head=0,seen=new Set([key(sx,sy)+',0']),dirs=rotatedAIDirs(p),margin=cfg.margin;
 while(head<queue.length){let index=queue[head++],n=nodes[index];if(!aiDangerDuring(p,forecast,n.x,n.y,n.t,endTime,margin))return reconstructTimed(nodes,index);if(n.step>=maxSteps)continue;
   let actions=allowWait?dirs.concat([[0,0]]):dirs;for(const [dx,dy] of actions){let x=n.x+dx,y=n.y+dy,nt=n.t+step,ns=n.step+1,moving=!!(dx||dy);if(moving){if(!walkable(p,x,y)||forecast.bombBlocked(x,y,nt,p))continue;if(n.step>0&&aiDangerDuring(p,forecast,n.x,n.y,n.t,n.t+step*.42,margin))continue;if(aiDangerDuring(p,forecast,x,y,n.t+step*.25,nt,margin))continue}else if(aiDangerDuring(p,forecast,x,y,n.t,nt,margin))continue;
     let sk=key(x,y)+','+ns;if(seen.has(sk))continue;seen.add(sk);nodes.push({x,y,step:ns,t:nt,parent:index});queue.push(nodes.length-1);
   }
 }
 return null;
}
function exploreSafeRoutes(p,forecast,cfg){
 let sx=Math.floor(p.x),sy=Math.floor(p.y),step=aiStepTime(p),nodes=[{x:sx,y:sy,d:0,t:0,parent:-1,first:null}],queue=[0],head=0,seen=new Set([key(sx,sy)]),dirs=rotatedAIDirs(p);
 while(head<queue.length){let index=queue[head++],n=nodes[index];if(n.d>=cfg.lookahead)continue;for(const [dx,dy] of dirs){let x=n.x+dx,y=n.y+dy,k=key(x,y),nt=n.t+step;if(seen.has(k)||!walkable(p,x,y)||forecast.bombBlocked(x,y,nt,p))continue;if(n.d>0&&aiDangerDuring(p,forecast,n.x,n.y,n.t,n.t+step*.4,cfg.margin))continue;if(aiDangerDuring(p,forecast,x,y,n.t+step*.22,nt,cfg.margin))continue;seen.add(k);nodes.push({x,y,d:n.d+1,t:nt,parent:index,first:n.first||{x,y}});queue.push(nodes.length-1)}}
 return nodes;
}
function routeFromExploration(nodes,index){let route=[];while(index>=0&&nodes[index].parent>=0){route.push({x:nodes[index].x,y:nodes[index].y,t:nodes[index].t});index=nodes[index].parent}return route.reverse()}
function syncAIWaypoint(p){
 let old=p.aiWaypoint;if(old){let n=p.aiRoute[p.aiRouteIndex],valid=old.centering?(old.x===Math.floor(p.x)&&old.y===Math.floor(p.y)):(n&&old.x===n.x&&old.y===n.y),distance=Math.hypot(old.x+.5-p.x,old.y+.5-p.y);if(valid&&distance>=.075)return true}p.aiWaypoint=null;
 while(p.aiRouteIndex<p.aiRoute.length){let n=p.aiRoute[p.aiRouteIndex],sx=Math.floor(p.x),sy=Math.floor(p.y),offCenter=Math.hypot(sx+.5-p.x,sy+.5-p.y);if(n.x===sx&&n.y===sy){let due=p.aiRouteStarted+(n.t||0);if(offCenter>=.075){p.aiWaypoint={x:sx,y:sy,centering:true};return true}if(time+0.015<due)return true;p.aiRouteIndex++;continue}if(offCenter>=.11){p.aiWaypoint={x:sx,y:sy,centering:true};return true}p.aiWaypoint={x:n.x,y:n.y};p.aiLastDir={x:n.x-sx,y:n.y-sy};return true}p.aiRouteLock=false;return false;
}
function clearAIRoute(p){p.aiRoute=[];p.aiRouteIndex=0;p.aiRouteStarted=time;p.aiRouteLock=false;p.aiGoal=null;p.aiWaypoint=null;p.aiProgressKey=null;p.aiProgressDistance=Infinity;p.aiProgressAt=time}
function abandonAIRoute(p,reason){let hadRoute=p.aiRouteLock||p.aiWaypoint||p.aiRouteIndex<p.aiRoute.length;clearAIRoute(p);if(hadRoute){p.aiStats.recoveries=(p.aiStats.recoveries||0)+1;p.aiLastRecovery={reason,at:time,tile:[Math.floor(p.x),Math.floor(p.y)]}}}
function setAIRoute(p,route,mode,goal=null){p.aiRoute=route||[];p.aiRouteIndex=0;p.aiRouteStarted=time;p.aiRouteLock=goal==='安全區'||goal==='爆彈逃生';p.aiMode=mode;p.aiGoal=goal;p.aiWaypoint=null;p.aiProgressKey=null;p.aiProgressDistance=Infinity;p.aiProgressAt=time;syncAIWaypoint(p)}
function lockedAIRouteHasThreat(p,forecast){if(!p.aiRouteLock||p.flamepass)return false;let cells=[{x:Math.floor(p.x),y:Math.floor(p.y)}].concat(p.aiRoute.slice(p.aiRouteIndex));return cells.some(c=>forecast.windows.has(key(c.x,c.y)))}
function lockedAIRouteWaiting(p){if(!p.aiRouteLock)return false;let n=p.aiRoute[p.aiRouteIndex],sx=Math.floor(p.x),sy=Math.floor(p.y);return!!n&&n.x===sx&&n.y===sy&&time+.015<p.aiRouteStarted+(n.t||0)}
function aiWaypointStalled(p){let w=p.aiWaypoint;if(!w){p.aiProgressKey=null;p.aiProgressDistance=Infinity;p.aiProgressAt=time;return false}let token=`${w.x},${w.y},${w.centering?'置中':'移動'}`,distance=Math.hypot(w.x+.5-p.x,w.y+.5-p.y);if(p.aiProgressKey!==token){p.aiProgressKey=token;p.aiProgressDistance=distance;p.aiProgressAt=time;return false}if(distance<p.aiProgressDistance-.025){p.aiProgressDistance=distance;p.aiProgressAt=time;return false}return time-p.aiProgressAt>.9}
function lockedAIRouteSafe(p,forecast,cfg){
 if(!p.aiRouteLock||p.aiRouteIndex>=p.aiRoute.length)return false;let x=Math.floor(p.x),y=Math.floor(p.y),t=0,step=aiStepTime(p);
 for(let i=p.aiRouteIndex;i<p.aiRoute.length;i++){let n=p.aiRoute[i],moving=n.x!==x||n.y!==y;t+=step;if(moving&&(!walkable(p,n.x,n.y)||forecast.bombBlocked(n.x,n.y,t,p)))return false;if(aiDangerDuring(p,forecast,n.x,n.y,Math.max(0,t-step*.7),t,cfg.margin))return false;x=n.x;y=n.y}
 return!aiDangerDuring(p,forecast,x,y,t,Math.min(forecast.horizon,Math.max(t,forecast.maxEnd)),cfg.margin);
}
function aiWaypointOpen(p,w){if(!w||w.centering)return true;let b=bombAt(w.x,w.y);return walkable(p,w.x,w.y)&&(!b||p.bombpass||b.passers.has(p.id))}
function followAIWaypoint(p){
 let sx=Math.floor(p.x),sy=Math.floor(p.y),hasExit=AI_DIRS.some(([dx,dy])=>{let x=sx+dx,y=sy+dy,b=bombAt(x,y);return walkable(p,x,y)&&(!b||p.bombpass||b.passers.has(p.id))});if(!hasExit)return{dx:0,dy:0};if(!syncAIWaypoint(p))return{dx:0,dy:0};let w=p.aiWaypoint;if(!w||!aiWaypointOpen(p,w))return{dx:0,dy:0};let cx=w.x+.5,cy=w.y+.5,rx=cx-p.x,ry=cy-p.y;if(Math.hypot(rx,ry)<.075){if(!w.centering)p.aiRouteIndex++;syncAIWaypoint(p);w=p.aiWaypoint;if(!w||!aiWaypointOpen(p,w))return{dx:0,dy:0};cx=w.x+.5;cy=w.y+.5;rx=cx-p.x;ry=cy-p.y}
 if(Math.abs(rx)>Math.abs(ry))return{dx:clamp(rx*5,-1,1),dy:0};return{dx:0,dy:clamp(ry*5,-1,1)};
}
function powerUtility(p,id){switch(id){case'bomb':return p.bombsMax<4?1.35:.5;case'fire':return p.range<5?1.25:.55;case'speed':return p.speed<4.2?1.3:.55;case'heart':return p.hp<3?1.55:.45;case'shield':return p.shield<2?1.5:.55;case'flamepass':return p.flamepass?.2:1.8;case'remote':return p.remote?.3:1.55;case'kick':return p.kick?.35:1.25;case'glove':return p.glove?.4:1.2;case'bombpass':return p.bombpass?.35:1.15;case'wallpass':return p.wallpass?.35:1.1;case'dash':return 1.15;case'mega':case'cluster':case'freeze':return 1.35;case'mystery':return .8;default:return 1}}
function bombOpportunity(p,x,y,cfg){
 let spec=hypotheticalBomb(p,x,y),sim=grid.map(r=>r.slice()),shape=blastShape(spec,sim);for(const c of shape.destroyed)if(sim[c.y]?.[c.x]===2)sim[c.y][c.x]=0;let cells=shape.main.slice();if(spec.cluster)for(const e of shape.endpoints)for(const [dx,dy] of [[1,1],[-1,1],[1,-1],[-1,-1]])if(sim[e.y+dy]?.[e.x+dx]===0)cells.push({x:e.x+dx,y:e.y+dy});let blast=new Set(cells.map(c=>key(c.x,c.y))),boxes=new Set(shape.destroyed.map(c=>key(c.x,c.y))).size,direct=0,trap=0,pressure=0;
 for(const q of players){if(q===p||!q.alive)continue;let qx=Math.floor(q.x),qy=Math.floor(q.y),hit=blast.has(key(qx,qy));if(!hit&&cfg.lookahead>=20){let px=qx+q.facing.x,py=qy+q.facing.y;hit=walkable(q,px,py)&&blast.has(key(px,py))}if(hit){direct++;let exits=AI_DIRS.filter(([dx,dy])=>walkable(q,qx+dx,qy+dy)&&!blast.has(key(qx+dx,qy+dy))&&!(qx+dx===x&&qy+dy===y)).length;trap+=Math.max(0,2-exits)}else{let nearest=Math.min(...cells.map(c=>Math.abs(c.x-qx)+Math.abs(c.y-qy)));if(nearest===1)pressure++}}
 return{boxes,direct,trap,pressure,score:boxes*5.2+(direct*8.5+trap*3.4+pressure*1.2)*(.45+cfg.aggression)};
}
function evaluateBombPlacement(p,x,y,cfg){
 let tag=`假想爆彈-${p.id}`,extra=hypotheticalBomb(p,x,y,tag),horizon=Math.max(cfg.horizon,extra.fuse+1.05),forecast=buildDangerForecast(horizon,[extra]),detonation=forecast.timeForTag(tag),end=Math.min(horizon,detonation+.82+cfg.margin),escape=findSurvivalRoute(p,forecast,end,cfg,false);
 if(escape===null){p.aiStats.bombsRejected++;return null}let opportunity=bombOpportunity(p,x,y,cfg),blast=new Set(forecast.cellsForTag(tag).map(c=>key(c.x,c.y))),trapBonus=0;
 for(const q of players){if(q===p||!q.alive||q.flamepass)continue;let qx=Math.floor(q.x),qy=Math.floor(q.y);if(!blast.has(key(qx,qy)))continue;let enemyEscape=findSurvivalRoute(q,forecast,end,{...cfg,margin:Math.max(.02,cfg.margin*.6)});if(enemyEscape===null)trapBonus+=8;else trapBonus+=Math.max(0,3-enemyEscape.filter((n,i,a)=>i===0||n.x!==a[i-1].x||n.y!==a[i-1].y).length)}
 p.aiStats.bombsVerified++;return{escape,forecast,detonation,opportunity,score:opportunity.score+trapBonus,trapBonus};
}
function considerRemoteDetonation(p,cfg){
 if(!p.remote||p.aiBombCd>0)return false;let own=bombs.filter(b=>b.owner===p&&!b.dead&&!b.ghost&&!b.airborne).sort((a,b)=>a.born-b.born)[0];if(!own)return false;let forced=new Map([[own,.04]]),forecast=buildDangerForecast(Math.max(1.1,cfg.horizon),[],forced),blast=new Set(forecast.cellsForRef(own).map(c=>key(c.x,c.y))),victims=players.filter(q=>q!==p&&q.alive&&blast.has(key(Math.floor(q.x),Math.floor(q.y))));if(!victims.length)return false;
 let sx=Math.floor(p.x),sy=Math.floor(p.y),selfSafe=p.flamepass||!forecast.dangerDuring(sx,sy,0,.9,cfg.margin);if(!selfSafe)return false;let value=victims.reduce((n,q)=>n+2+(q.shield?0:1)+(q.hp===1?1:0),0);if(value<2.5+(1-cfg.aggression)*1.5||Math.random()<cfg.mistake)return false;
 p.aiQueuedSkill=true;p.aiBombCd=.62;p.aiMode='遙控伏擊';p.aiStats.trapsPlanned++;return true;
}
function emergencyBombControl(p,forecast){
 let sx=Math.floor(p.x),sy=Math.floor(p.y);for(const [dx,dy] of rotatedAIDirs(p)){let b=bombAt(sx+dx,sy+dy);if(!b||b.fuse<.3||!canBombEnter(sx+dx*2,sy+dy*2,b))continue;let mode=p.glove&&!(p.remote&&bombs.some(o=>o.owner===p&&!o.dead))?'拳套救援':'踢彈救援';setAIRoute(p,[{x:sx+dx,y:sy+dy,t:aiStepTime(p)}],mode,'爆彈救援');p.aiLastDir={x:dx,y:dy};if(mode==='拳套救援')p.aiQueuedSkill=true;return p.glove||p.kick}return false;
}
function chooseAIObjective(p,forecast,cfg,leaveCurrent=false){
 let nodes=exploreSafeRoutes(p,forecast,cfg),visibleItems=items.filter(it=>!it.dead),scored=[];
 nodes.forEach((n,index)=>{let score=-n.d*.34,mode='巡弋',goal=key(n.x,n.y),item=visibleItems.find(it=>Math.floor(it.x)===n.x&&Math.floor(it.y)===n.y),opp=bombOpportunity(p,n.x,n.y,cfg);if(item){score+=14*powerUtility(p,item.id)+Math.min(4,item.life*.12);mode='蒐集晶片';goal='晶片:'+goal}
   if(opp.boxes){score+=opp.boxes*(2.2+cfg.aggression);if(mode==='巡弋')mode='破牆開路'}if(opp.direct||opp.trap){score+=(opp.direct*5+opp.trap*3)*cfg.aggression;mode=opp.trap?'封路伏擊':'追擊對手'}else if(opp.pressure){score+=opp.pressure*cfg.aggression;mode='逼迫走位'}
   let exits=AI_DIRS.filter(([dx,dy])=>walkable(p,n.x+dx,n.y+dy)&&!forecast.bombBlocked(n.x+dx,n.y+dy,n.t+aiStepTime(p),p)).length;score+=Math.min(3,exits)*.22;if(leaveCurrent&&n.d===0)score-=20;let visit=p.aiVisited.get(key(n.x,n.y));if(visit!=null)score-=Math.max(0,2.4-(time-visit))*.9;if(p.aiGoal===goal)score+=1.45;
   let enemies=players.filter(q=>q!==p&&q.alive);if(enemies.length){let nearest=Math.min(...enemies.map(q=>Math.abs(Math.floor(q.x)-n.x)+Math.abs(Math.floor(q.y)-n.y)));score+=(cfg.aggression-.35)*Math.max(0,7-nearest)*.3}scored.push({score,index,mode,goal});
 });
 scored.sort((a,b)=>b.score-a.score||nodes[a.index].d-nodes[b.index].d);if(!scored.length){setAIRoute(p,[],'觀察戰局');return}let pick=0;if(scored.length>1&&Math.random()<cfg.mistake)pick=Math.min(scored.length-1,1+Math.floor(rand(Math.min(3,scored.length-1))));let chosen=scored[pick],route=routeFromExploration(nodes,chosen.index);setAIRoute(p,route,chosen.mode,chosen.goal);
}
function decideAI(p,cfg,forecast=null){
 p.aiStats.decisions++;let sx=Math.floor(p.x),sy=Math.floor(p.y),visitKey=key(sx,sy);p.aiVisited.set(visitKey,time);if(p.aiVisited.size>70)for(const [k,t] of p.aiVisited)if(time-t>12)p.aiVisited.delete(k);
 forecast=forecast||buildDangerForecast(cfg.horizon);let eta=p.flamepass?Infinity:forecast.nextDanger(sx,sy,0);p.aiDangerETA=eta;
 if(eta<=cfg.escapeLead){let end=Math.min(cfg.horizon,Math.max(.9,forecast.maxEnd)),escape=findSurvivalRoute(p,forecast,end,cfg);if(escape?.length){setAIRoute(p,escape,eta<.5?'緊急閃避':'預判逃生','安全區');p.aiStats.escapes++;let remoteWouldFire=p.remote&&bombs.some(b=>b.owner===p&&!b.dead&&!b.ghost&&!b.airborne);if(p.dash>0&&eta<.68&&!remoteWouldFire)p.aiQueuedSkill=true;return}if(escape===null&&emergencyBombControl(p,forecast))return;if(escape===null){setAIRoute(p,[],'受困求生');return}}
 if(considerRemoteDetonation(p,cfg))return;
 let centered=Math.abs(p.x-(sx+.5))<.14&&Math.abs(p.y-(sy+.5))<.14,canPlant=centered&&p.aiBombCd<=0&&p.bombsLive<p.bombsMax&&!bombReservationAt(sx,sy)&&eta>Math.min(.75,cfg.escapeLead),leaveCurrent=false;
 if(canPlant){let plan=evaluateBombPlacement(p,sx,sy,cfg),threshold=4+(1-cfg.aggression)*1.5;if(plan&&plan.score>=threshold&&Math.random()>=cfg.mistake){p.aiQueuedBomb=true;p.aiBombCd=.88+(1-cfg.aggression)*.35;p.aiEscapeUntil=time+plan.detonation+.85;setAIRoute(p,plan.escape,plan.trapBonus>3?'封鎖伏擊':plan.opportunity.boxes?'爆破開路':'戰術爆破','爆彈逃生');if(plan.trapBonus>3)p.aiStats.trapsPlanned++;return}leaveCurrent=!plan}
 chooseAIObjective(p,forecast,cfg,leaveCurrent);
}
function replanBombEscape(p,forecast,cfg){let end=Math.min(forecast.horizon,Math.max(.9,forecast.maxEnd)),escape=findSurvivalRoute(p,forecast,end,cfg,false);if(escape?.length){setAIRoute(p,escape,'強制逃生','爆彈逃生');p.aiStats.escapes++;return true}if(escape===null){setAIRoute(p,[],'受困求生');return false}setAIRoute(p,[],'安全待命');return true}
function thinkAI(p,dt){
 let cfg=AI_LEVELS[p.aiLevel]||AI_LEVELS.normal;p.aiThink-=dt;if(p.aiThink<=0){
   p.aiThink=rand(cfg.thinkMax,cfg.thinkMin);let forcedEscape=p.aiGoal==='爆彈逃生'&&time<p.aiEscapeUntil,horizon=Math.max(cfg.horizon,forcedEscape?Math.min(10,p.aiEscapeUntil-time+.25):0),forecast=buildDangerForecast(horizon),replanned=false;
   if(lockedAIRouteWaiting(p)&&!lockedAIRouteHasThreat(p,forecast)){abandonAIRoute(p,'危險解除');if(forcedEscape)p.aiEscapeUntil=0;forcedEscape=false}
   let w=p.aiWaypoint,step=aiStepTime(p),blockedWaypoint=w&&!w.centering&&(!walkable(p,w.x,w.y)||forecast.bombBlocked(w.x,w.y,step,p)),stalled=!blockedWaypoint&&aiWaypointStalled(p);if(blockedWaypoint||stalled){abandonAIRoute(p,blockedWaypoint?'目標受阻':'移動無進展');if(forcedEscape){replanBombEscape(p,forecast,cfg);replanned=true}}
   let keepLocked=p.aiRouteLock&&lockedAIRouteSafe(p,forecast,cfg),committed=p.aiWaypoint&&Math.hypot(p.aiWaypoint.x+.5-p.x,p.aiWaypoint.y+.5-p.y)>=.075;if(!replanned&&forcedEscape&&!committed&&p.aiRouteIndex>=p.aiRoute.length){replanBombEscape(p,forecast,cfg);replanned=true}if(!replanned&&!committed&&!keepLocked)decideAI(p,cfg,forecast)
 }let move=followAIWaypoint(p),bomb=!!p.aiQueuedBomb,skill=!!p.aiQueuedSkill;p.aiQueuedBomb=false;p.aiQueuedSkill=false;if(bomb)p.aiThink=0;
 // 反向詛咒會在 updatePlayers 再翻一次；AI 先補償，維持實際路徑不變。
 if(p.reverse>0){move.dx=-move.dx;move.dy=-move.dy}return{dx:move.dx,dy:move.dy,bomb,skill};
}
function dangerTiles(horizon=2.5){let forecast=buildDangerForecast(horizon),m=new Map();for(const [k,list] of forecast.windows)m.set(k,list[0]?.start??0);return m}
function hasEscapeAfterBomb(p,x,y){let cfg=AI_LEVELS[p.aiLevel]||AI_LEVELS.normal,tag='逃生測試',extra=hypotheticalBomb(p,x,y,tag),horizon=Math.max(cfg.horizon,extra.fuse+1),forecast=buildDangerForecast(horizon,[extra]),end=forecast.timeForTag(tag)+.82+cfg.margin;return findSurvivalRoute(p,forecast,end,cfg,false)!==null}
function blastContains(b,x,y){let pos=forecastBombCell(b),spec={x:pos.x,y:pos.y,range:b.range,mega:b.mega,pierce:b.pierce},shape=blastShape(spec,grid);return shape.main.some(c=>c.x===x&&c.y===y)}
function blastHitsEnemy(b,p){return players.some(q=>q!==p&&q.alive&&blastContains(b,Math.floor(q.x),Math.floor(q.y)))}
