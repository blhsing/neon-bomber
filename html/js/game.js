'use strict';
function startGame(){
 let active=slots.filter(s=>s!=='off').length;if(active<2){flashWarning('至少需要兩位參賽者才能點燃競技場');return}unlockAudio();sfx.click();targetWins=+$('#wins').value;density=+$('#density').value;loot=+$('#loot').value;for(let i=0;i<4;i++)playerNames[i]=normalizePlayerName(playerNames[i],i);saveSettings();round=1;let humanIndex=0;players=slots.map((s,i)=>makePlayer(i,s,s==='human'?humanIndex++:-1)).filter(Boolean);state='playing';winner=null;$('#menu').classList.add('hidden');$('#topbar').classList.remove('hidden');$('#announcement').classList.add('hidden');newRound();canvas.focus();
}
function makePlayer(i,type,controlIndex=-1){if(type==='off')return null;return{id:i,type,controlIndex,aiLevel:aiLevels[i],color:COLORS[i],name:normalizePlayerName(playerNames[i],i),wins:0,x:1.5,y:1.5,vx:0,vy:0,facing:{x:0,y:1},moveDir:null,moveIdle:Infinity,turnBuffer:null,alive:true,ghost:false,ghostGeneration:0,ghostTrack:0,ghostBomb:null,ghostThink:0,ghostAim:null,hp:1,inv:0,stun:0,frozen:0,bombsMax:1,bombsLive:0,range:1,speed:3.15,kick:false,glove:false,remote:false,disguise:false,pierce:false,bombpass:false,wallpass:false,flamepass:false,shield:0,dash:0,dashing:0,magnet:false,mega:0,cluster:0,reverse:0,slow:0,aiThink:0,aiGoal:null,aiWaypoint:null,aiRoute:[],aiRouteIndex:0,aiRouteStarted:0,aiRouteLock:false,aiMode:'待命',aiDangerETA:Infinity,aiBombCd:0,aiQueuedBomb:false,aiQueuedSkill:false,aiEscapeUntil:0,aiLastDir:null,aiVisited:new Map(),aiProgressKey:null,aiProgressDistance:Infinity,aiProgressAt:0,aiLastRecovery:null,aiStats:{decisions:0,escapes:0,bombsVerified:0,bombsRejected:0,trapsPlanned:0,recoveries:0},trail:[],stats:{bombs:0,boxes:0,ghostThrows:0,revivals:0}}}
function newRound(){
 bombs=[];flames=[];items=[];particles=[];texts=[];shockwaves=[];freezeWorld=0;roundTimer=0;roundLocked=false;winner=null;buildArena();
 const spawns=[[1.5,1.5],[COLS-1.5,ROWS-1.5],[COLS-1.5,1.5],[1.5,ROWS-1.5]];
 players.forEach(p=>{let s=spawns[p.id];Object.assign(p,{x:s[0],y:s[1],vx:0,vy:0,moveDir:null,moveIdle:Infinity,turnBuffer:null,alive:true,ghost:false,ghostGeneration:0,ghostTrack:0,ghostBomb:null,ghostThink:0,ghostAim:null,hp:1,inv:2.2,stun:0,frozen:0,bombsMax:1,bombsLive:0,range:1,speed:3.15,kick:false,glove:false,remote:false,disguise:false,pierce:false,bombpass:false,wallpass:false,flamepass:false,shield:0,dash:0,dashing:0,magnet:false,mega:0,cluster:0,reverse:0,slow:0,aiThink:rand(.1,0),aiGoal:null,aiWaypoint:null,aiRoute:[],aiRouteIndex:0,aiRouteStarted:time,aiRouteLock:false,aiMode:'待命',aiDangerETA:Infinity,aiBombCd:1,aiQueuedBomb:false,aiQueuedSkill:false,aiEscapeUntil:0,aiLastDir:null,aiVisited:new Map(),aiProgressKey:null,aiProgressDistance:Infinity,aiProgressAt:time,aiLastRecovery:null,lastHit:null,trail:[]})});
 $('#roundLabel').textContent=`第 ${round} 回合　・　先取 ${targetWins} 冠`;state='playing';paused=false;$('#pause').classList.add('hidden');
 for(let i=0;i<50;i++)particles.push({x:rand(W),y:rand(H),vx:rand(.2,-.2),vy:rand(.25,-.25),life:rand(12,5),max:12,size:rand(2,.4),color:i%2?'#38f7ff':'#ff47c8',bg:true});
}
function buildArena(){
 grid=Array.from({length:ROWS},(_,y)=>Array.from({length:COLS},(_,x)=>(x===0||y===0||x===COLS-1||y===ROWS-1||(x%2===0&&y%2===0))?1:0));
 // 起點保留可轉彎的 L 形逃生袋；初始射程為 1，讓首枚爆彈更容易安全練習。
 const safe=new Set();[[1,1,1,1],[COLS-2,ROWS-2,-1,-1],[COLS-2,1,-1,1],[1,ROWS-2,1,-1]].forEach(([x,y,h,v])=>[[0,0],[h,0],[h*2,0],[0,v],[0,v*2],[h*2,v],[h,v*2]].forEach(([dx,dy])=>safe.add(key(x+dx,y+dy))));
 for(let y=1;y<ROWS-1;y++)for(let x=1;x<COLS-1;x++)if(!grid[y][x]&&!safe.has(key(x,y))&&Math.random()<density)grid[y][x]=2;
}

function blocked(x,y,p,ignoreBomb=null,axis=null){
 const r=.31;
 // Along a movement axis, collide on the player's centreline. This keeps walls
 // solid head-on while allowing a corner to slide past once over half of the
 // player has cleared the perpendicular edge.
 const pts=axis==='x'?[[x-r,y],[x+r,y]]:axis==='y'?[[x,y-r],[x,y+r]]:[[x-r,y-r],[x+r,y-r],[x-r,y+r],[x+r,y+r]];
 for(const [px,py] of pts){let tx=Math.floor(px),ty=Math.floor(py);if(tx<0||ty<0||tx>=COLS||ty>=ROWS)return true;let g=grid[ty][tx];if(g===1||(g===2&&!p.wallpass))return true;
   let b=bombAt(tx,ty);if(b&&b!==ignoreBomb&&!p.bombpass&&!b.passers.has(p.id))return true;}
 return false;
}
function playerOverlapsCell(p,x,y,r=.31){return p.x+r>x&&p.x-r<x+1&&p.y+r>y&&p.y-r<y+1}
const GHOST_TRACK_W=COLS-1,GHOST_TRACK_H=ROWS-1,GHOST_TRACK_LENGTH=GHOST_TRACK_W*2+GHOST_TRACK_H*2,GHOST_SPEED=4.4;
function wrapGhostTrack(t){return((t%GHOST_TRACK_LENGTH)+GHOST_TRACK_LENGTH)%GHOST_TRACK_LENGTH}
function ghostPoint(track){let t=wrapGhostTrack(track);if(t<=GHOST_TRACK_W)return{x:.5+t,y:.5,segment:'top',inward:{x:0,y:1}};t-=GHOST_TRACK_W;if(t<=GHOST_TRACK_H)return{x:COLS-.5,y:.5+t,segment:'right',inward:{x:-1,y:0}};t-=GHOST_TRACK_H;if(t<=GHOST_TRACK_W)return{x:COLS-.5-t,y:ROWS-.5,segment:'bottom',inward:{x:0,y:-1}};t-=GHOST_TRACK_W;return{x:.5,y:ROWS-.5-t,segment:'left',inward:{x:1,y:0}}}
function ghostTrackFromPosition(x,y){let choices=[{d:y-.5,t:clamp(x-.5,0,GHOST_TRACK_W)},{d:COLS-.5-x,t:GHOST_TRACK_W+clamp(y-.5,0,GHOST_TRACK_H)},{d:ROWS-.5-y,t:GHOST_TRACK_W+GHOST_TRACK_H+GHOST_TRACK_W-clamp(x-.5,0,GHOST_TRACK_W)},{d:x-.5,t:GHOST_TRACK_LENGTH-clamp(y-.5,0,GHOST_TRACK_H)}];return choices.sort((a,b)=>a.d-b.d)[0].t}
function ghostTrackDelta(from,to){let d=wrapGhostTrack(to)-wrapGhostTrack(from);if(d>GHOST_TRACK_LENGTH/2)d-=GHOST_TRACK_LENGTH;if(d<-GHOST_TRACK_LENGTH/2)d+=GHOST_TRACK_LENGTH;return d}
function moveGhost(p,dx,dy,dt){let mag=Math.hypot(dx,dy);if(!mag)return;if(Math.abs(dx)>Math.abs(dy))p.facing={x:Math.sign(dx),y:0};else p.facing={x:0,y:Math.sign(dy)};dx/=mag;dy/=mag;let cur=ghostPoint(p.ghostTrack),eps=.06,plus=ghostPoint(p.ghostTrack+eps),minus=ghostPoint(p.ghostTrack-eps),plusScore=dx*(plus.x-cur.x)+dy*(plus.y-cur.y),minusScore=dx*(minus.x-cur.x)+dy*(minus.y-cur.y),dir=plusScore>minusScore?1:-1,speed=GHOST_SPEED*(p.speed/3.15)*(p.dashing?2.55:1);if(Math.max(plusScore,minusScore)>.003)p.ghostTrack=wrapGhostTrack(p.ghostTrack+dir*speed*dt);let pos=ghostPoint(p.ghostTrack);p.x=pos.x;p.y=pos.y}
function bombReservationAt(x,y,exclude=null){return bombs.some(b=>b!==exclude&&!b.dead&&(b.airborne?(b.airborne.toX===x&&b.airborne.toY===y):(Math.floor(b.x)===x&&Math.floor(b.y)===y)))}
function ghostLandingCellsAt(track,facing,exclude=null){let pos=ghostPoint(track),cells=[];if(pos.segment==='top'&&facing.x===0&&facing.y===1){let x=clamp(Math.floor(pos.x),1,COLS-2);for(let y=1;y<ROWS-1;y++)cells.push({x,y})}else if(pos.segment==='bottom'&&facing.x===0&&facing.y===-1){let x=clamp(Math.floor(pos.x),1,COLS-2);for(let y=ROWS-2;y>=1;y--)cells.push({x,y})}else if(pos.segment==='left'&&facing.x===1&&facing.y===0){let y=clamp(Math.floor(pos.y),1,ROWS-2);for(let x=1;x<COLS-1;x++)cells.push({x,y})}else if(pos.segment==='right'&&facing.x===-1&&facing.y===0){let y=clamp(Math.floor(pos.y),1,ROWS-2);for(let x=COLS-2;x>=1;x--)cells.push({x,y})}return cells.filter(c=>grid[c.y]?.[c.x]===0&&!bombReservationAt(c.x,c.y,exclude))}
function makeBombSource(owner,ghost=false){return{id:++bombSerial,owner,ghost,ghostGeneration:ghost?owner.ghostGeneration:null,reviveUsed:false}}
function ghostBombsFor(p){return bombs.filter(b=>!b.dead&&b.ghost&&b.owner===p)}
function throwGhostBomb(p){if(roundLocked||!p.ghost||p.alive||players.filter(q=>q.alive).length<2||ghostBombsFor(p).length>=p.bombsMax)return false;let rail=ghostPoint(p.ghostTrack);p.facing={...rail.inward};let cells=ghostLandingCellsAt(p.ghostTrack,p.facing);if(!cells.length)return false;let cell=cells[Math.floor(rand(cells.length))],source=makeBombSource(p,true),airborne={fromX:p.x,fromY:p.y,toX:cell.x,toY:cell.y,cells:cells.map(c=>({...c})),elapsed:0,duration:.48},initialFuse=p.remote?REMOTE_BOMB_FUSE:GHOST_BOMB_FUSE,b={x:p.x,y:p.y,owner:p,source,range:p.range,fuse:initialFuse,initialFuse,dead:false,moving:null,airborne,passers:new Set(),mega:p.mega>0,cluster:p.cluster>0,pierce:p.pierce,disguised:p.disguise,ghost:true,born:time};if(b.mega)p.mega--;if(b.cluster)p.cluster--;bombs.push(b);p.stats.ghostThrows=(p.stats.ghostThrows||0)+1;sfx.kick();burst(p.x,p.y,p.color,18,2.4);return true}
function useGhostSkill(p){if(p.remote){let b=ghostBombsFor(p).filter(b=>!b.airborne).sort((a,b)=>a.born-b.born)[0];if(b)explodeBomb(b);return}if(p.dash>0&&!p.dashing){p.dash--;p.dashing=.38;sfx.kick();burst(p.x,p.y,p.color,14,2.2)}}
function ghostFiringPosts(target,cell=null){let x=clamp(Math.floor(cell?.x??target.x),1,COLS-2),y=clamp(Math.floor(cell?.y??target.y),1,ROWS-2);return[{track:x,facing:{x:0,y:1}},{track:GHOST_TRACK_W+y,facing:{x:-1,y:0}},{track:GHOST_TRACK_W+GHOST_TRACK_H+GHOST_TRACK_W-x,facing:{x:0,y:-1}},{track:GHOST_TRACK_LENGTH-y,facing:{x:1,y:0}}]}
const GHOST_AI_TUNING={
 novice:{hitWeight:1.15,travelWeight:.9,noise:.72,thinkScale:1.8,hold:0,switchMargin:0,reverseMargin:0,forecast:[1,0,0]},
 normal:{hitWeight:2.45,travelWeight:.46,noise:.16,thinkScale:1.15,hold:.22,switchMargin:.08,reverseMargin:.08,forecast:[1,0,0]},
 expert:{hitWeight:3.7,travelWeight:.28,noise:.045,thinkScale:.85,hold:.78,switchMargin:.18,reverseMargin:.3,forecast:[.35,.65,0]},
 insane:{hitWeight:4.9,travelWeight:.15,noise:.008,thinkScale:.58,hold:1.12,switchMargin:.28,reverseMargin:.52,forecast:[.15,.65,.2]}
};
function ghostForecastCells(target,tuning){let cells=[],add=(x,y,weight)=>{if(weight<=0)return;let old=cells.find(c=>c.x===x&&c.y===y);if(old)old.weight+=weight;else cells.push({x,y,weight})},x=clamp(Math.floor(target.x),1,COLS-2),y=clamp(Math.floor(target.y),1,ROWS-2),weights=tuning.forecast,pathOpen=true;add(x,y,weights[0]);for(let step=1;step<=2;step++){let weight=weights[step];if(weight<=0)continue;let nx=x+target.facing.x*step,ny=y+target.facing.y*step,b=bombAt(nx,ny);pathOpen=pathOpen&&walkable(target,nx,ny)&&(!b||target.bombpass||b.passers.has(target.id));if(pathOpen)add(nx,ny,weight);else add(x,y,weight)}let sum=cells.reduce((n,c)=>n+c.weight,0)||1;cells.forEach(c=>c.weight/=sum);return cells}
function ghostPostScore(p,target,post,forecast,tuning,withNoise=true){let cells=ghostLandingCellsAt(post.track,post.facing);if(!cells.length)return null;let blasts=cells.map(cell=>new Set(blastShape({x:cell.x,y:cell.y,range:p.range,mega:p.mega>0,pierce:p.pierce},grid).main.map(c=>key(c.x,c.y)))),hitChance=0;for(const aim of forecast){let aimKey=key(aim.x,aim.y),hits=blasts.reduce((n,shape)=>n+(shape.has(aimKey)?1:0),0);hitChance+=aim.weight*hits/cells.length}let distance=Math.abs(ghostTrackDelta(p.ghostTrack,post.track)),travel=distance/(GHOST_TRACK_LENGTH/2),baseScore=hitChance*tuning.hitWeight-travel*tuning.travelWeight,rankScore=baseScore+(withNoise?rand(tuning.noise,-tuning.noise):0);return{...post,target,hitChance,distance,baseScore,rankScore}}
function sameGhostPost(a,b){return!!a&&!!b&&a.target===b.target&&Math.abs(ghostTrackDelta(a.track,b.track))<.01&&a.facing.x===b.facing.x&&a.facing.y===b.facing.y}
function thinkGhostAI(p,dt){p.ghostThink-=dt;let living=players.filter(q=>q.alive);if(living.length<2)return{dx:0,dy:0,bomb:false,skill:false};if(p.ghostThink<=0||!p.ghostAim||!p.ghostAim.target.alive){let level=GHOST_AI_TUNING[p.aiLevel]?p.aiLevel:'normal',cfg=AI_LEVELS[level],tuning=GHOST_AI_TUNING[level],old=p.ghostAim,candidates=[],seen=new Set();for(const target of living){let forecast=ghostForecastCells(target,tuning);for(const aim of forecast)for(const post of ghostFiringPosts(target,aim)){let k=`${target.id}:${post.track}:${post.facing.x},${post.facing.y}`;if(seen.has(k))continue;seen.add(k);let candidate=ghostPostScore(p,target,post,forecast,tuning);if(candidate)candidates.push(candidate)}}candidates.sort((a,b)=>b.rankScore-a.rankScore||a.distance-b.distance);let chosen=candidates[0]||null;if(chosen&&old?.target?.alive&&tuning.hold>0){let oldForecast=ghostForecastCells(old.target,tuning),current=ghostPostScore(p,old.target,old,oldForecast,tuning,false);if(current&&current.hitChance>0){let age=time-(old.chosenAt??time),oldDir=Math.sign(ghostTrackDelta(p.ghostTrack,current.track)),newDir=Math.sign(ghostTrackDelta(p.ghostTrack,chosen.track)),reversing=oldDir&&newDir&&oldDir!==newDir,margin=tuning.switchMargin+(reversing?tuning.reverseMargin:0);if(age<tuning.hold||chosen.baseScore<current.baseScore+margin)chosen=current}}if(chosen){chosen.chosenAt=sameGhostPost(old,chosen)?old.chosenAt??time:time;p.ghostAim=chosen}else p.ghostAim=null;p.ghostThink=rand(cfg.thinkMax*tuning.thinkScale,cfg.thinkMin*tuning.thinkScale)}if(!p.ghostAim){p.aiMode='幽靈待命';return{dx:0,dy:0,bomb:false,skill:false}}let d=ghostTrackDelta(p.ghostTrack,p.ghostAim.track);if(Math.abs(d)>.16){p.aiMode='幽靈追蹤';let cur=ghostPoint(p.ghostTrack),next=ghostPoint(p.ghostTrack+Math.sign(d)*.08),skill=!p.remote&&p.dash>0&&!p.dashing&&Math.abs(d)>3;return{dx:next.x-cur.x,dy:next.y-cur.y,bomb:false,skill}}p.ghostTrack=wrapGhostTrack(p.ghostAim.track);let pos=ghostPoint(p.ghostTrack);p.x=pos.x;p.y=pos.y;p.facing={...p.ghostAim.facing};let active=ghostBombsFor(p),useRemote=p.remote&&active.length>=p.bombsMax&&active.some(b=>!b.airborne),canThrow=active.length<p.bombsMax&&!active.some(b=>b.airborne);p.aiMode=useRemote?'幽靈遙控引爆':canThrow?'幽靈伏擊':'幽靈等待爆破';return{dx:p.facing.x,dy:p.facing.y,bomb:canThrow,skill:useRemote}}
function bombAt(x,y){return bombs.find(b=>!b.dead&&!b.airborne&&Math.floor(b.x)===x&&Math.floor(b.y)===y)}
function nextLaneCenter(value,direction){return(direction>0?Math.ceil(value-.500001):Math.floor(value-.499999))+.5}
function tryPlayerStep(p,dir,distance){
 if(distance<=0)return true;let nx=p.x+dir.x*distance,ny=p.y+dir.y*distance,axis=dir.x?'x':'y';
 if(!blocked(nx,ny,p,null,axis)){p.x=nx;p.y=ny;return true}
 tryKick(p,dir.x,dir.y);return false;
}
function recordPlayerTrail(p){p.trail.push({x:p.x,y:p.y,life:.28});if(p.trail.length>10)p.trail.shift()}
const TURN_BUFFER_HEADING_LIFETIME=.18;
function cardinalMoveDirection(dx,dy){return Math.abs(dx)>.05&&Math.abs(dy)<=.05?{x:Math.sign(dx),y:0}:Math.abs(dy)>.05&&Math.abs(dx)<=.05?{x:0,y:Math.sign(dy)}:null}
function rememberPlayerMovement(p,dir){p.moveDir={x:dir.x,y:dir.y};p.moveIdle=0}
function beginBufferedTurn(p,turn,speed){
 let along={...p.moveDir},targetX=along.x?nextLaneCenter(p.x,along.x):p.x,targetY=along.y?nextLaneCenter(p.y,along.y):p.y,distance=Math.hypot(targetX-p.x,targetY-p.y);
 p.turnBuffer={x:turn.x,y:turn.y,along,targetX,targetY,time:distance/Math.max(speed,.01)+.22};
}
function advanceBufferedTurn(p,speed,dt){
 let b=p.turnBuffer;if(!b)return false;b.time-=dt;if(b.time<=0){p.turnBuffer=null;return false}
 let distance=Math.hypot(b.targetX-p.x,b.targetY-p.y),travel=Math.min(distance,speed*dt),remaining=dt;
 if(travel>0){if(!tryPlayerStep(p,b.along,travel)){p.turnBuffer=null;return true}rememberPlayerMovement(p,b.along);remaining=Math.max(0,dt-travel/speed)}
 if(distance-travel<=.0001){p.x=b.targetX;p.y=b.targetY;p.turnBuffer=null;p.facing={x:b.x,y:b.y};if(remaining>.0001&&tryPlayerStep(p,{x:b.x,y:b.y},speed*remaining))rememberPlayerMovement(p,{x:b.x,y:b.y})}
 return true;
}
function movePlayer(p,dx,dy,dt){
 if(!p.alive||p.stun>0||p.frozen>0)return;p.moveIdle=(p.moveIdle??Infinity)+dt;let mag=Math.hypot(dx,dy),speed=p.speed*(p.slow>0?.62:1)*(p.dashing?2.55:1);
 // Preserve the AI's proportional slowdown near a waypoint. Normalizing every nonzero
 // vector made fast AIs overshoot the center, reverse, and oscillate until their fuse ran out.
 if(mag>1){dx/=mag;dy/=mag}
 if(p.turnBuffer){
   let b=p.turnBuffer,requested=cardinalMoveDirection(dx,dy),perpendicular=!!requested&&requested.x*b.along.x+requested.y*b.along.y===0;
   if(!perpendicular)p.turnBuffer=null;
   else{b.x=requested.x;b.y=requested.y;advanceBufferedTurn(p,speed,dt);recordPlayerTrail(p);return}
 }
 if(!mag){p.turnBuffer=null;return}
 let previous=p.moveDir,requested=cardinalMoveDirection(dx,dy),turn=p.type==='human'&&requested&&previous&&p.moveIdle<=TURN_BUFFER_HEADING_LIFETIME&&requested.x*previous.x+requested.y*previous.y===0?requested:null,step=speed*dt;
 if(turn){
   let nx=p.x+turn.x*step,ny=p.y+turn.y*step,axis=turn.x?'x':'y',partiallyBlocked=blocked(nx,ny,p,null,axis)||blocked(nx,ny,p);
   if(partiallyBlocked){beginBufferedTurn(p,turn,speed);advanceBufferedTurn(p,speed,dt)}
   else if(tryPlayerStep(p,turn,step)){rememberPlayerMovement(p,turn);p.facing={...turn}}
   recordPlayerTrail(p);return;
 }
 if(Math.abs(dx)>.15||Math.abs(dy)>.15)p.facing=Math.abs(dx)>Math.abs(dy)?{x:Math.sign(dx),y:0}:{x:0,y:Math.sign(dy)};
 // Grid-centering makes corridors feel crisp without removing analogue diagonal motion.
 if(Math.abs(dx)>.1&&Math.abs(dy)<.1){let cy=Math.floor(p.y)+.5;if(Math.abs(p.y-cy)<.19)p.y+=(cy-p.y)*Math.min(1,dt*13)}
 if(Math.abs(dy)>.1&&Math.abs(dx)<.1){let cx=Math.floor(p.x)+.5;if(Math.abs(p.x-cx)<.19)p.x+=(cx-p.x)*Math.min(1,dt*13)}
 let movedX=Math.abs(dx)>.001&&tryPlayerStep(p,{x:Math.sign(dx),y:0},Math.abs(dx)*step),movedY=Math.abs(dy)>.001&&tryPlayerStep(p,{x:0,y:Math.sign(dy)},Math.abs(dy)*step);
 if(movedX||movedY)rememberPlayerMovement(p,movedX&&(!movedY||Math.abs(dx)>Math.abs(dy))?{x:Math.sign(dx),y:0}:{x:0,y:Math.sign(dy)});
 recordPlayerTrail(p);
}
function tryKick(p,dx,dy){if(!p.kick||(!dx&&!dy))return;let tx=Math.floor(p.x+dx*.65),ty=Math.floor(p.y+dy*.65),b=bombAt(tx,ty);if(b&&!b.moving){let nx=tx+dx,ny=ty+dy;if(canBombEnter(nx,ny)){b.moving={x:dx,y:dy};b.passers.add(p.id);sfx.kick();}}}
function canBombEnter(x,y,b=null){return x>0&&y>0&&x<COLS-1&&y<ROWS-1&&grid[y][x]===0&&!bombReservationAt(x,y,b)}
function placeBomb(p){
 if(!p.alive||p.stun>0||p.frozen>0||p.bombsLive>=p.bombsMax)return false;let x=Math.floor(p.x),y=Math.floor(p.y);if(bombReservationAt(x,y))return false;
 // Every player already overlapping the tile may leave it. Restricting this exemption to the owner traps stacked players inside a bomb they did not place.
 let passers=new Set(players.filter(q=>q.alive&&playerOverlapsCell(q,x,y)).map(q=>q.id)),initialFuse=p.remote?REMOTE_BOMB_FUSE:NORMAL_BOMB_FUSE,b={x:x+.5,y:y+.5,owner:p,source:makeBombSource(p,false),range:p.range,fuse:initialFuse,initialFuse,dead:false,moving:null,airborne:null,passers,mega:p.mega>0,cluster:p.cluster>0,pierce:p.pierce,disguised:p.disguise,ghost:false,born:time};if(b.mega)p.mega--;if(b.cluster)p.cluster--;bombs.push(b);p.bombsLive++;p.stats.bombs++;sfx.place();burst(b.x,b.y,p.color,8,.8);return true;
}
function useSkill(p){
 if(!p.alive)return;
 if(p.remote){let b=bombs.filter(b=>b.owner===p&&!b.dead&&!b.ghost&&!b.airborne).sort((a,b)=>a.born-b.born)[0];if(b){explodeBomb(b);return}}
 if(p.glove){let tx=Math.floor(p.x+p.facing.x*.8),ty=Math.floor(p.y+p.facing.y*.8),b=bombAt(tx,ty);if(b){throwBomb(b,p.facing.x,p.facing.y);return}}
 if(p.dash>0&&!p.dashing){p.dash--;p.dashing=.38;p.inv=Math.max(p.inv,.2);sfx.kick();burst(p.x,p.y,p.color,14,2.2)}
}
function throwBomb(b,dx,dy){if(!dx&&!dy)return;let x=Math.floor(b.x),y=Math.floor(b.y),last={x,y};for(let i=0;i<4;i++){let nx=x+dx,ny=y+dy;if(!canBombEnter(nx,ny,b))break;x=nx;y=ny;last={x,y}}b.x=last.x+.5;b.y=last.y+.5;b.moving=null;sfx.kick();burst(b.x,b.y,'#d98cff',11,1.6)}

function humanActions(p){let dx=0,dy=0,bomb=false,skill=false,c=CONTROLS[p.controlIndex],held=b=>b.some(k=>keys.has(k)),tapped=b=>b.some(k=>pressed.has(k));if(c){dy+=(held(c.down)?1:0)-(held(c.up)?1:0);dx+=(held(c.right)?1:0)-(held(c.left)?1:0);bomb=tapped(c.bomb);skill=tapped(c.skill)}return{dx,dy,bomb,skill}}
function updatePlayers(dt){
 for(const p of players){p.inv=Math.max(0,p.inv-dt);p.stun=Math.max(0,p.stun-dt);p.frozen=Math.max(0,p.frozen-dt);p.reverse=Math.max(0,p.reverse-dt);p.slow=Math.max(0,p.slow-dt);p.aiBombCd=Math.max(0,p.aiBombCd-dt);if(p.dashing)p.dashing=Math.max(0,p.dashing-dt);if(!p.alive){if(!p.ghost)continue;let a=p.type==='human'?humanActions(p):thinkGhostAI(p,dt);moveGhost(p,a.dx,a.dy,dt);if(a.bomb)throwGhostBomb(p);if(a.skill)useGhostSkill(p);continue}
   let a=p.type==='human'?humanActions(p):thinkAI(p,dt),dx=a.dx,dy=a.dy,bomb=a.bomb,skill=a.skill;
   if(p.reverse>0){dx=-dx;dy=-dy}if(bomb)placeBomb(p);movePlayer(p,dx,dy,dt);if(skill)useSkill(p);
    // 角色的整個碰撞盒離開後，炸彈才重新變成實心；只看中心格會在格線上把角色夾住。
    bombs.forEach(b=>{if(!b.passers.has(p.id))return;let bx=Math.floor(b.x),by=Math.floor(b.y);if(!playerOverlapsCell(p,bx,by))b.passers.delete(p.id)});
   collectItems(p,dt);
 }
}
/*
 * 人工智慧以「秒」為單位預測危險，不再把整條炸線永久標成禁區。
 * 每枚炸彈先算實際引爆時間；若較早的火焰碰到另一枚炸彈，反覆提前
 * 後者的時間，直到連鎖穩定。模擬也依時間拆箱，因此後發爆炸會看見
 * 已被前一波清空的通道。所有戰術只使用目前可見的地圖、玩家與晶片。
 */
function walkable(p,x,y){if(x<1||y<1||x>=COLS-1||y>=ROWS-1)return false;let g=grid[y][x];return g===0||(g===2&&p.wallpass)}
function aiStepTime(p){let speed=p.speed*(p.slow>0?.62:1)*(p.dashing?2.55:1);return clamp(1/Math.max(1.8,speed),.18,.48)}
function rotatedAIDirs(p){let n=p.id%AI_DIRS.length,dirs=AI_DIRS.slice(n).concat(AI_DIRS.slice(0,n));if(p.aiLastDir){let i=dirs.findIndex(d=>d[0]===p.aiLastDir.x&&d[1]===p.aiLastDir.y);if(i>0)dirs.unshift(...dirs.splice(i,1))}return dirs}
