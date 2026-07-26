'use strict';
const $=s=>document.querySelector(s), canvas=$('#game'), ctx=canvas.getContext('2d');
const CC0_MEDIA={
 bomb:'assets/images/bomb.png',
 click:'assets/audio/click.ogg',
 pickup:'assets/audio/pickup.ogg',
 win:'assets/audio/win.ogg'
};
const cc0BombSprite=new Image();cc0BombSprite.src=CC0_MEDIA.bomb;
const cc0Audio={};
function playCC0(id,volume=.18){if(!audioOn||!CC0_MEDIA[id])return;let source=cc0Audio[id]||(cc0Audio[id]=new Audio(CC0_MEDIA[id])),voice=source.cloneNode();voice.volume=volume;voice.play().catch(()=>{})}
const W=1280,H=720,COLS=17,ROWS=13,TILE=48,OX=32,OY=50,AW=COLS*TILE,AH=ROWS*TILE;
const NORMAL_BOMB_FUSE=1.9,REMOTE_BOMB_FUSE=8,GHOST_BOMB_FUSE=1.9;
const MAGNET_ATTRACTION_RADIUS=2.5;
let canvasCssScale=1;
function syncCanvasCssScale(){let r=canvas.getBoundingClientRect();canvasCssScale=Math.max(.001,Math.min(r.width/W,r.height/H))}
function setCanvasFont(weight,size){ctx.font=`${weight} ${Math.max(size,12/canvasCssScale)}px "Microsoft JhengHei"`}
new ResizeObserver(syncCanvasCssScale).observe(canvas);window.addEventListener('resize',syncCanvasCssScale);syncCanvasCssScale();
const COLORS=['#38f7ff','#ff47c8','#ffd23f','#77ff57'];
const NAMES=['青藍閃電','桃紅魅影','黃金戰神','翠綠風暴'];
let playerNames=NAMES.slice();
const CONTROLS=[
 {up:['KeyW'],left:['KeyA'],down:['KeyS'],right:['KeyD'],bomb:['KeyQ'],skill:['KeyE'],label:'W A S D｜Q 爆彈・E 技能'},
 {up:['KeyU'],left:['KeyH'],down:['KeyJ'],right:['KeyK'],bomb:['KeyY'],skill:['KeyI'],label:'U H J K｜Y 爆彈・I 技能'},
 {up:['ArrowUp'],left:['ArrowLeft'],down:['ArrowDown'],right:['ArrowRight'],bomb:['PageUp'],skill:['PageDown'],label:'方向鍵｜Page Up 爆彈・Page Down 技能'}
];
const AI_LEVELS={
 novice:{label:'新手',thinkMin:.27,thinkMax:.43,horizon:2.8,lookahead:8,mistake:.24,aggression:.28,escapeLead:1.45,margin:.015},
 normal:{label:'標準',thinkMin:.14,thinkMax:.24,horizon:4.4,lookahead:14,mistake:.09,aggression:.50,escapeLead:2.35,margin:.07},
 expert:{label:'高手',thinkMin:.075,thinkMax:.135,horizon:6.4,lookahead:21,mistake:.025,aggression:.72,escapeLead:4.2,margin:.12},
 insane:{label:'瘋狂',thinkMin:.035,thinkMax:.075,horizon:9.2,lookahead:30,mistake:.004,aggression:.94,escapeLead:9.2,margin:.17}
};
const AI_DIRS=[[1,0],[-1,0],[0,1],[0,-1]];
const POWERS={
 bomb:{name:'爆彈袋',color:'#ffdb54',desc:'可同時多放一枚爆彈',weight:30},
 fire:{name:'烈焰核心',color:'#ff653d',desc:'爆炸範圍增加一格',weight:30},
 speed:{name:'疾風輪',color:'#5dffb0',desc:'移動速度永久提升',weight:10},
 kick:{name:'戰靴',color:'#50b9ff',desc:'推動並踢飛碰到的爆彈',weight:7},
 glove:{name:'重力拳套',color:'#d98cff',desc:'技能鍵拋出前方爆彈',weight:5},
 remote:{name:'遙控器',color:'#ff4ba7',desc:'技能鍵引爆最早的自家爆彈',weight:5},
 disguise:{name:'擬態模組',color:'#c247ff',desc:'本回合爆彈偽裝成能量箱，爆風虛線仍會示警',weight:4},
 pierce:{name:'電漿針',color:'#d7fbff',desc:'烈焰可貫穿一個能量箱',weight:4},
 bombpass:{name:'虛相靴',color:'#aa91ff',desc:'可以穿過靜止爆彈',weight:4},
 wallpass:{name:'量子鑽',color:'#cb7dff',desc:'可以穿過能量箱',weight:3},
 flamepass:{name:'鳳凰甲',color:'#ffac4d',desc:'永久免疫一般烈焰',weight:2},
 shield:{name:'光子護盾',color:'#62dfff',desc:'抵擋下一次傷害',weight:8},
 heart:{name:'生命晶核',color:'#ff6585',desc:'增加一顆生命，最多三顆',weight:5},
 dash:{name:'脈衝引擎',color:'#7dfffb',desc:'技能鍵高速衝刺，可充能',weight:5},
 mega:{name:'超新星',color:'#ffcf55',desc:'下一枚爆彈巨大且威力加倍',weight:4},
 cluster:{name:'蜂群核心',color:'#8aff63',desc:'下一枚爆彈散射額外火花',weight:4},
 freeze:{name:'零度脈衝',color:'#80d5ff',desc:'立刻冰凍所有對手片刻',weight:3},
 magnet:{name:'磁力場',color:'#ff79e2',desc:'約 2.5 格內的晶片會自動飛向你',weight:4},
 mystery:{name:'混沌禮盒',color:'#f8f8ff',desc:'隨機神力、瞬移、反向或減速',weight:6}
};
const EFFECT_COLORS={reverse:'#ff72cf',slow:'#92d858'};
const SETTINGS_KEY='霓虹爆彈王-玩家設定-v1',slots=['human','ai','ai','off'],aiLevels=['normal','normal','expert','insane']; let targetWins=3, density=.60, loot=.52;
let state='menu',paused=false,last=0,time=0,round=1,grid=[],players=[],bombs=[],flames=[],items=[],particles=[],texts=[],shockwaves=[],camera={x:0,y:0,shake:0},winner=null,roundTimer=0,freezeWorld=0,roundLocked=false,bombSerial=0;
let keys=new Set(),pressed=new Set(),audioOn=true,audio=null,noiseBuffer=null,musicTimer=0,musicStep=0;
const rand=(a=1,b=0)=>Math.random()*(a-b)+b, clamp=(v,a,b)=>Math.max(a,Math.min(b,v)), dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const tile=(x,y)=>({x:Math.floor(x),y:Math.floor(y)}), key=(x,y)=>x+','+y;
const escapeHTML=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function normalizePlayerName(value,i){let name=[...String(value??'').trim()].slice(0,10).join('');return name||NAMES[i]}
function saveSettings(){try{localStorage.setItem(SETTINGS_KEY,JSON.stringify({slots:[...slots],aiLevels:[...aiLevels],playerNames:[...playerNames],targetWins,density,loot,audioOn}))}catch{}}
function selectNumber(id,value){let select=$(id),option=[...select.options].find(o=>+o.value===+value);if(option)select.value=option.value}
function loadSettings(){try{let saved=JSON.parse(localStorage.getItem(SETTINGS_KEY)||'null');if(saved&&typeof saved==='object'){if(Array.isArray(saved.slots))for(let i=0;i<4;i++)if(['human','ai','off'].includes(saved.slots[i]))slots[i]=saved.slots[i];let humans=0;for(let i=0;i<4;i++)if(slots[i]==='human'&&++humans>3)slots[i]='ai';if(Array.isArray(saved.aiLevels))for(let i=0;i<4;i++)if(AI_LEVELS[saved.aiLevels[i]])aiLevels[i]=saved.aiLevels[i];if(Array.isArray(saved.playerNames))for(let i=0;i<4;i++)playerNames[i]=normalizePlayerName(saved.playerNames[i],i);if([1,2,3,5].includes(+saved.targetWins))targetWins=+saved.targetWins;if([.46,.60,.72].includes(+saved.density))density=+saved.density;if([.30,.52,.76].includes(+saved.loot))loot=+saved.loot;if(typeof saved.audioOn==='boolean')audioOn=saved.audioOn}}catch{}selectNumber('#wins',targetWins);selectNumber('#density',density);selectNumber('#loot',loot);$('#soundBtn').textContent=`音效：${audioOn?'開':'關'}`}

function renderSlots(){
 const root=$('#slots');root.innerHTML='';
 slots.forEach((type,i)=>{const el=document.createElement('div');el.className='slot '+(type==='off'?'off':'');el.style.setProperty('--pc',COLORS[i]);
   let controlIndex=slots.slice(0,i).filter(s=>s==='human').length;
   let detail=type==='human'?CONTROLS[controlIndex].label:type==='ai'?'會預測連鎖、規劃逃生、搶晶片與封路':'此席不參賽';
   let aiChoice=type==='ai'?`<label class="ai-choice">戰術等級<select class="ai-level" data-ai-slot="${i}" aria-label="參賽者 ${i+1} 人工智慧難度">${Object.entries(AI_LEVELS).map(([id,d])=>`<option value="${id}"${aiLevels[i]===id?' selected':''}>${d.label}</option>`).join('')}</select></label>`:'';
   el.innerHTML=`<div class="slot-head"><i class="orb"></i><b>參賽者 ${i+1}</b></div><label class="name-choice">名稱<input class="player-name" data-name-slot="${i}" maxlength="10" value="${escapeHTML(playerNames[i])}" aria-label="參賽者 ${i+1} 名稱"></label><button class="type" data-slot="${i}">${type==='human'?'真人':type==='ai'?'人工智慧':'關閉'}</button>${aiChoice}<div class="keys">${detail.replace(/([A-Z])/g,'<span class="kbd">$1</span>')}</div>`;root.appendChild(el)});
 root.querySelectorAll('.type').forEach(b=>b.onclick=()=>cycleSlot(+b.dataset.slot));
 root.querySelectorAll('.ai-level').forEach(s=>s.onchange=()=>{aiLevels[+s.dataset.aiSlot]=s.value;saveSettings()});
 root.querySelectorAll('.player-name').forEach(input=>{let i=+input.dataset.nameSlot;input.oninput=()=>{playerNames[i]=[...input.value].slice(0,10).join('');saveSettings()};input.onblur=()=>{playerNames[i]=normalizePlayerName(input.value,i);input.value=playerNames[i];saveSettings()}});
}
function cycleSlot(i){let choices=['human','ai','off'];let n=(choices.indexOf(slots[i])+1)%choices.length;let next=choices[n];if(next==='human'&&slots.filter(x=>x==='human').length>=3){next='ai';flashWarning('同一把鍵盤最多支援三位真人參賽者');}slots[i]=next;saveSettings();renderSlots();}
function flashWarning(s){$('#warning').textContent=s;clearTimeout(flashWarning.t);flashWarning.t=setTimeout(()=>$('#warning').textContent='',2600)}
function buildHelp(){
 $('#controlHelp').innerHTML=CONTROLS.map((c,i)=>`<div class="help-item"><div class="help-icon" style="--ic:${COLORS[i]}">${i+1}</div><div><b>真人 ${i+1}</b><span>${c.label}</span></div></div>`).join('');
 const powerRoot=$('#powerHelp');powerRoot.innerHTML=Object.entries(POWERS).map(([id,p])=>`<div class="help-item"><canvas class="help-icon" data-power="${id}" width="64" height="64" style="--ic:${p.color}" aria-hidden="true"></canvas><div><b>${p.name}</b><span>${p.desc}</span></div></div>`).join('');
 powerRoot.querySelectorAll('[data-power]').forEach(c=>{let id=c.dataset.power;drawPowerIcon(c.getContext('2d'),id,32,32,47,POWERS[id].color)});
}
function showHelp(v){$('#help').classList.toggle('hidden',!v)}
