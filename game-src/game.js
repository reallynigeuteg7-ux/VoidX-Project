import * as THREE from 'three';
import {mergeGeometries} from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import {clamp,circleBlocked,moveCircle,clearLine,flowField,WEAPONS} from './core.mjs';
import {visualMaterials,lighting,dressArena,weapon,operative,radialTexture} from './visuals.mjs';

const $=id=>document.getElementById(id), show=id=>$(id).classList.remove('hidden'), hide=id=>$(id).classList.add('hidden');
let saved={};try{saved=JSON.parse(localStorage.getItem('voidx.preferences')||'{}');}catch{}
const prefs={sensitivity:50,volume:65,quality:'medium',firstWeapon:0,best:0,...saved};
function save(){try{localStorage.setItem('voidx.preferences',JSON.stringify(prefs));}catch{}}
const touch=navigator.maxTouchPoints>0||matchMedia('(pointer:coarse)').matches;
document.body.classList.toggle('touch',touch);
let renderer;
try {renderer=new THREE.WebGLRenderer({canvas:$('scene'),antialias:true,alpha:false,powerPreference:'high-performance'});}catch(e){show('error');throw e;}
renderer.setClearColor(0x8e8e8e);renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.info.autoReset=false;
const scene=new THREE.Scene();scene.fog=new THREE.FogExp2(0x8b8b8b,.012);
const camera=new THREE.PerspectiveCamera(76,innerWidth/innerHeight,.06,160);camera.rotation.order='YXZ';
const gunScene=new THREE.Scene(), gunCamera=new THREE.PerspectiveCamera(65,innerWidth/innerHeight,.01,10);
scene.add(new THREE.HemisphereLight(0xe0e0e0,0x252525,.85));gunScene.add(new THREE.HemisphereLight(0xffffff,0x252525,.8));
const sun=new THREE.DirectionalLight(0xffffff,2.0);sun.position.set(-12,30,12);scene.add(sun);
const gunLight=new THREE.DirectionalLight(0xffffff,2.3);gunLight.position.set(-2,3,2);gunScene.add(gunLight);
const mats=visualMaterials(renderer);lighting(renderer,scene,gunScene,sun);
const boxGeo=new THREE.BoxGeometry(1,1,1),colliders=[],wallMeshes=[];
function box(parent,x,y,z,w,h,d,mat=mats.concrete){const m=new THREE.Mesh(boxGeo,mat);m.position.set(x,y,z);m.scale.set(w,h,d);m.castShadow=true;m.receiveShadow=true;parent.add(m);return m;}
function solid(x,z,w,h,d,mat=mats.concrete){const m=box(scene,x,h/2,z,w,h,d,mat);colliders.push({x,z,w,d});wallMeshes.push(m);return m;}
function cylinder(parent,x,y,z,r,h,mat,rotX=0){const m=new THREE.Mesh(new THREE.CylinderGeometry(r,r,h,12),mat);m.position.set(x,y,z);m.rotation.x=rotX;m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
let seed=1942;function random(){seed=(seed*1664525+1013904223)>>>0;return seed/4294967296;}
const floor=box(scene,0,-.17,0,65,.3,65,mats.floor);floor.castShadow=false;
// Perimeter: a derelict industrial courtyard, kept open through its centre.
solid(-25,0,2,9,52);solid(25,0,2,10,52);solid(0,-25,50,11,2);solid(0,25,50,7,2);
for(const side of [-1,1])for(let z=-20;z<=20;z+=8){
 box(scene,side*23.86,4.5,z,.16,.18,4,mats.dark);box(scene,side*23.84,5.9,z,.18,2.3,3.4,mats.black);
 box(scene,side*23.73,5.9,z,.16,2.3,.11,mats.metal);box(scene,side*23.73,5.9,z,.16,.1,3.4,mats.metal);
 box(scene,side*24.3,5,z+3.8,.85,10,.6,mats.dark);
}
for(let x=-20;x<=20;x+=8){box(scene,x,6.2,-23.87,3.7,2.7,.16,mats.black);box(scene,x,6.2,-23.74,.12,2.7,.1,mats.metal);box(scene,x,6.2,-23.74,3.7,.1,.1,mats.metal);box(scene,x,9.8,-24.2,4.8,.2,.8,mats.dark);}
// Distant silhouettes stay outside the collision arena.
for(let i=0;i<36;i++){let angle=i/36*Math.PI*2,r=37+random()*13,h=10+random()*20;box(scene,Math.cos(angle)*r,h/2-1,Math.sin(angle)*r,3+random()*7,h,3+random()*7,mats.dark);if(i%3===0)cylinder(scene,Math.cos(angle)*r,h+2,Math.sin(angle)*r,.12,10,mats.dark);}
for(const [x,z,w,d] of [[-11,-9,5,3],[10,-10,4,4],[-10,8,4,3],[10,9,5,3],[-18,0,3,7],[18,0,3,7]]){
 solid(x,z,w,2.2,d,mats.concrete);box(scene,x,2.27,z,w+.12,.14,d+.12,mats.light);
 for(let a=-w/2+.5;a<w/2;a+=.75)box(scene,x+a,1.1,z+d/2+.01,.08,1.8,.035,mats.dark);
}
for(const [x,z] of [[-5,-18],[6,-17],[-16,17],[17,18]]){
 solid(x,z,2.2,2.5,2.2,mats.metal);box(scene,x,2.53,z,2.3,.15,2.3,mats.dark);box(scene,x,1.25,z+1.11,.12,2.5,.04,mats.light);
 box(scene,x,1.25,z+1.13,2.2,.12,.04,mats.light);
}
// Street markings, drains, small rubble and overhead pipes.
for(let z=-20;z<24;z+=5){box(scene,-2.8,.011,z,.075,.018,2.2,mats.light);box(scene,2.8,.011,z,.075,.018,2.2,mats.light);}
for(let z=-20;z<=20;z+=4){box(scene,-21,.012,z,1.2,.02,.6,mats.dark);for(let x=-21.5;x<-20.5;x+=.17)box(scene,x,.025,z,.045,.02,.58,mats.metal);}
for(let i=0;i<100;i++){let x=(random()-.5)*46,z=(random()-.5)*46;if(Math.abs(x)<4)continue;const m=box(scene,x,.08,z,.1+random()*.4,.08+random()*.15,.1+random()*.3,i%2?mats.dark:mats.concrete);m.rotation.y=random()*Math.PI;}
for(const x of [-20,20]){cylinder(scene,x,5,-15,.12,10,mats.metal);box(scene,x,9.9,-12,.16,.16,6,mats.metal);box(scene,x,9.7,-9,1,.15,.6,mats.white);}
function sign(text,x,y,z,w,h){const c=document.createElement('canvas');c.width=512;c.height=256;const a=c.getContext('2d');a.fillStyle='#272727';a.fillRect(0,0,512,256);a.strokeStyle='#777';a.lineWidth=6;a.strokeRect(12,12,488,232);a.fillStyle='#c5c5c5';a.font='bold 116px Arial';a.textAlign='center';a.fillText(text,256,158);a.font='18px monospace';a.fillText('RESTRICTED / VOID SECTOR',256,211);const t=new THREE.CanvasTexture(c);const m=new THREE.Mesh(new THREE.PlaneGeometry(w,h),new THREE.MeshStandardMaterial({map:t,roughness:1}));m.position.set(x,y,z);scene.add(m);}
sign('07',0,5,-23.8,6,3);sign('VOID',-15,2.6,-23.8,4,2);
 dressArena(scene,mats,box,cylinder,colliders);
// Batch static geometry by material: the detailed arena uses a handful of draw calls.
function batchMeshes(parent){const groups=new Map();parent.updateMatrixWorld(true);for(const mesh of [...parent.children]){if(!mesh.isMesh)continue;mesh.updateMatrix();let geometry=mesh.geometry.index?mesh.geometry.toNonIndexed():mesh.geometry.clone();geometry.applyMatrix4(mesh.matrix);const key=mesh.material.uuid+'-'+mesh.castShadow+'-'+mesh.receiveShadow;if(!groups.has(key))groups.set(key,{material:mesh.material,geometries:[],cast:mesh.castShadow,receive:mesh.receiveShadow});groups.get(key).geometries.push(geometry);parent.remove(mesh);}for(const {material,geometries,cast,receive} of groups.values()){const merged=mergeGeometries(geometries);const mesh=new THREE.Mesh(merged,material);mesh.castShadow=cast;mesh.receiveShadow=receive;parent.add(mesh);geometries.forEach(g=>g.dispose());}}
batchMeshes(scene);
// Fog-lit particles use one draw call.
const dustPos=new Float32Array(210*3);for(let i=0;i<210;i++){dustPos[i*3]=(random()-.5)*50;dustPos[i*3+1]=random()*10;dustPos[i*3+2]=(random()-.5)*50;}
const glowMap=radialTexture();const dustGeo=new THREE.BufferGeometry();dustGeo.setAttribute('position',new THREE.BufferAttribute(dustPos,3));const dust=new THREE.Points(dustGeo,new THREE.PointsMaterial({color:0xcccccc,map:glowMap,size:.065,transparent:true,depthWrite:false,opacity:.25}));scene.add(dust);

const weapons=new THREE.Group();gunScene.add(weapons);const gunModels=[];
function makeGun(type){const g=weapon(weapons,type,mats);gunModels.push(g);return g;}
makeGun(0);makeGun(1);gunModels.forEach(batchMeshes);
const flash=new THREE.Sprite(new THREE.SpriteMaterial({map:glowMap,color:0xffffff,blending:THREE.AdditiveBlending,depthWrite:false}));gunScene.add(flash);flash.visible=false;
const muzzleLight=new THREE.PointLight(0xffffff,0,2);gunScene.add(muzzleLight);
const fxGeo=new THREE.OctahedronGeometry(.024),fxMat=new THREE.MeshBasicMaterial({color:0xeeeeee});
const tracerGeo=new THREE.CylinderGeometry(.012,.019,.48,5);tracerGeo.rotateX(Math.PI/2);
const state={mode:'menu',player:{x:0,z:20,hp:100},yaw:0,pitch:0,wave:0,score:0,kills:0,time:0,shots:0,hits:0,weapon:prefs.firstWeapon,ammo:[30,8],reserve:[180,48],cooldown:0,reloading:0,reloadDuration:0,damage:0,recoil:0,flash:0,hit:0,spawnLeft:0,spawnTimer:0,between:0,announcement:0,move:0};
let enemies=[],projectiles=[],particles=[],pickups=[],feed=[],nav=flowField(state.player,colliders),navTimer=0,enemyId=0,audioCtx,noiseBuffer;
const input={keys:new Set(),moveX:0,moveY:0,fire:false,joyId:null,lookId:null,fireId:null,lastX:0,lastY:0};
function resetInput(){input.keys.clear();input.moveX=input.moveY=0;input.fire=false;input.joyId=input.lookId=input.fireId=null;$('stick').style.transform='';}
function sound(kind){if(!prefs.volume)return;try{audioCtx??=new (window.AudioContext||window.webkitAudioContext)();if(audioCtx.state==='suspended')audioCtx.resume();const t=audioCtx.currentTime,g=audioCtx.createGain();g.connect(audioCtx.destination);const v=prefs.volume/100;if(kind==='shot'||kind==='hurt'){
 if(!noiseBuffer){noiseBuffer=audioCtx.createBuffer(1,audioCtx.sampleRate*.35,audioCtx.sampleRate);const a=noiseBuffer.getChannelData(0);for(let i=0;i<a.length;i++)a[i]=Math.random()*2-1;}
 const n=audioCtx.createBufferSource();n.buffer=noiseBuffer;const f=audioCtx.createBiquadFilter();f.type='lowpass';f.frequency.value=kind==='hurt'?500:state.weapon?950:2200;n.connect(f);f.connect(g);g.gain.setValueAtTime(v*(kind==='hurt'?.22:.28),t);g.gain.exponentialRampToValueAtTime(.001,t+.15);n.start(t);n.stop(t+.18);n.onended=()=>{n.disconnect();f.disconnect();g.disconnect();};
 }else{const o=audioCtx.createOscillator();o.type='triangle';o.frequency.setValueAtTime(kind==='hit'?880:kind==='reload'?190:440,t);o.frequency.exponentialRampToValueAtTime(kind==='hit'?400:110,t+.12);o.connect(g);g.gain.setValueAtTime(v*.085,t);g.gain.exponentialRampToValueAtTime(.001,t+.15);o.start();o.stop(t+.16);o.onended=()=>{o.disconnect();g.disconnect();};}}catch{}}
function makeEnemy(x,z){const g=new THREE.Group();g.position.set(x,0,z);const e={id:++enemyId,mesh:g,x,z,hp:65+state.wave*8,speed:1.35+state.wave*.13,shoot:1.7+Math.random()*1.8,phase:Math.random()*6,flash:0};
 operative(g,e,mats);batchMeshes(g);batchMeshes(e.head);e.legs.forEach(batchMeshes);
 g.traverse(m=>{if(m.isMesh){m.userData.enemy=e;m.userData.head=m.parent===e.head;}});scene.add(g);enemies.push(e);return e;}
const spawns=[[-16,-18],[0,-20],[17,-17],[-21,-7],[21,-6],[-20,11],[20,12],[0,21],[-6,-20],[9,21]];
function spawn(){const choices=spawns.filter(([x,z])=>Math.hypot(x-state.player.x,z-state.player.z)>12&&!enemies.some(e=>Math.hypot(e.x-x,e.z-z)<2));const p=choices.length?choices[Math.floor(Math.random()*choices.length)]:spawns.reduce((a,b)=>Math.hypot(a[0]-state.player.x,a[1]-state.player.z)>Math.hypot(b[0]-state.player.x,b[1]-state.player.z)?a:b);makeEnemy(...p);state.spawnLeft--;}
function announce(s,t,p,duration=3){$('announceSmall').textContent=s;$('announceTitle').textContent=t;$('announceText').textContent=p;show('announcement');state.announcement=duration;}
function startWave(){state.wave++;state.spawnLeft=2+state.wave*2;state.spawnTimer=.3;state.between=0;announce('МЁРТВЫЙ СЕКТОР',`ВОЛНА 0${state.wave}`,state.wave===1?(touch?'Левый стик — движение. Справа — обзор и огонь.':'WASD — движение. Нажми на арену, чтобы захватить мышь.'):'Держи дистанцию. Используй укрытия.',3);updateHUD();}
function releaseMouse(){if(document.pointerLockElement)document.exitPointerLock();}
function lockMouse(){if(!touch&&state.mode==='playing'){try{const p=$('scene').requestPointerLock();p?.catch(()=>{});}catch{}}}
function disposeEnemy(e){e.mesh.traverse(m=>{if(m.geometry&&m.geometry!==boxGeo)m.geometry.dispose();});scene.remove(e.mesh);}
function clearActors(){enemies.forEach(disposeEnemy);for(const a of [...projectiles,...particles,...pickups])scene.remove(a.mesh);enemies=[];projectiles=[];particles=[];pickups=[];}
function startGame(){clearActors();resetInput();Object.assign(state,{mode:'playing',player:{x:0,z:20,hp:100},yaw:0,pitch:0,wave:0,score:0,kills:0,time:0,shots:0,hits:0,weapon:prefs.firstWeapon,ammo:[30,8],reserve:[180,48],cooldown:.3,reloading:0,damage:0,recoil:0,flash:0,hit:0,between:0,move:0});feed=[];nav=flowField(state.player,colliders);navTimer=0;for(const id of ['menu','pause','result','settings','arsenal'])hide(id);show('hud');startWave();selectWeapon(state.weapon);sound('start');lockMouse();}
function pause(){if(state.mode!=='playing')return;state.mode='paused';resetInput();releaseMouse();show('pause');hide('announcement');}
function resume(){hide('pause');state.mode='playing';resetInput();lockMouse();}
function menu(){state.mode='menu';resetInput();releaseMouse();for(const id of ['hud','pause','result','announcement'])hide(id);show('menu');clearActors();$('bestScore').textContent=String(prefs.best).padStart(5,'0');}
function finish(win){state.mode='result';resetInput();releaseMouse();hide('hud');hide('announcement');show('result');if(win)state.score+=1000;const isBest=state.score>prefs.best;if(isBest){prefs.best=state.score;save();}$('resultEyebrow').textContent=isBest?'НОВЫЙ ЛИЧНЫЙ РЕКОРД':'ОПЕРАЦИЯ ЗАВЕРШЕНА';$('resultTitle').textContent=win?'СЕКТОР ЧИСТ.':'СИГНАЛ ПОТЕРЯН.';$('resultText').textContent=win?'Пять волн позади. Сегодня пустота отступила.':`Волна ${state.wave} из 5. Пустота ждёт твоего возвращения.`;$('finalScore').textContent=String(state.score).padStart(5,'0');$('finalKills').textContent=String(state.kills).padStart(2,'0');$('finalTime').textContent=`${String(Math.floor(state.time/60)).padStart(2,'0')}:${String(Math.floor(state.time%60)).padStart(2,'0')}`;}
function selectWeapon(i){state.weapon=i;state.reloading=0;state.cooldown=.25;gunModels.forEach((g,n)=>g.visible=n===i);updateHUD();}
function reload(){const w=WEAPONS[state.weapon];if(state.mode!=='playing'||state.reloading||state.ammo[state.weapon]>=w.mag||state.reserve[state.weapon]<=0)return;state.reloading=state.reloadDuration=w.reload;sound('reload');updateHUD();}
function burst(pos,count=5){for(let i=0;i<count;i++){const m=new THREE.Mesh(fxGeo,fxMat);m.position.copy(pos);m.scale.setScalar(.3+Math.random()*.7);scene.add(m);particles.push({mesh:m,v:new THREE.Vector3((Math.random()-.5)*3,Math.random()*3,(Math.random()-.5)*3),life:.25+Math.random()*.25});}}
function dropMed(x,z){const g=new THREE.Group();box(g,0,0,0,.55,.35,.45,mats.dark);box(g,0,.18,0,.12,.012,.32,mats.light);box(g,0,.18,0,.36,.012,.1,mats.light);g.position.set(x,.25,z);scene.add(g);pickups.push({mesh:g,x,z,kind:'med'});}
function kill(e,head){disposeEnemy(e);enemies=enemies.filter(v=>v!==e);state.kills++;state.score+=head?150:100;burst(e.mesh.position.clone().add(new THREE.Vector3(0,1.2,0)),9);if(state.kills%3===0)dropMed(e.x,e.z);feed.unshift({text:head?'ТОЧНОЕ ПОПАДАНИЕ +150':'ЦЕЛЬ УСТРАНЕНА +100',life:3});feed=feed.slice(0,3);}
const ray=new THREE.Raycaster();const direction=new THREE.Vector3();
function shoot(){if(state.mode!=='playing'||state.cooldown>0||state.reloading)return;const n=state.weapon,w=WEAPONS[n];if(state.ammo[n]<=0){reload();return;}state.ammo[n]--;state.shots++;state.cooldown=w.interval;state.recoil=n?.065:.026;state.flash=.045;sound('shot');camera.updateMatrixWorld();
 const targets=wallMeshes.concat(enemies.map(e=>e.mesh));let didHit=false;
 for(let i=0;i<w.pellets;i++){direction.set((Math.random()-.5)*w.spread,(Math.random()-.5)*w.spread,-1).normalize().applyQuaternion(camera.quaternion);ray.set(camera.position,direction);ray.far=65;const hits=ray.intersectObjects(targets,true);if(hits.length){const h=hits[0],e=h.object.userData.enemy;if(e&&enemies.includes(e)){let head=h.object.userData.head===true;e.hp-=w.damage*(head?1.8:1);didHit=true;burst(h.point,2);if(e.hp<=0)kill(e,head);}else burst(h.point,2);}}
 if(didHit){state.hits++;state.hit=.12;sound('hit');}updateHUD();}
function hurt(amount){if(state.mode!=='playing')return;state.player.hp=Math.max(0,state.player.hp-amount);state.damage=.65;sound('hurt');if(state.player.hp<=0)finish(false);updateHUD();}
function updateHUD(){const n=state.weapon;$('wave').textContent=`ВОЛНА ${String(state.wave).padStart(2,'0')} / 05`;$('remaining').textContent=`ЦЕЛИ: ${String(enemies.length+state.spawnLeft).padStart(2,'0')}`;$('score').textContent=String(state.score).padStart(5,'0');$('health').textContent=Math.ceil(state.player.hp);$('healthFill').style.width=state.player.hp+'%';$('ammo').textContent=String(state.ammo[n]).padStart(2,'0');$('reserve').textContent=state.reserve[n];$('weaponName').textContent=WEAPONS[n].name;$('reloadLabel').textContent=state.reloading?'ПЕРЕЗАРЯДКА...':state.ammo[n]===0?'НУЖНА ПЕРЕЗАРЯДКА':'ГОТОВ К БОЮ';}

function update(dt){state.time+=dt;state.cooldown=Math.max(0,state.cooldown-dt);state.flash=Math.max(0,state.flash-dt);state.damage=Math.max(0,state.damage-dt*1.6);state.recoil=Math.max(0,state.recoil-dt*.16);state.hit=Math.max(0,state.hit-dt);
 if(state.reloading>0){state.reloading-=dt;if(state.reloading<=0){const n=state.weapon,k=Math.min(WEAPONS[n].mag-state.ammo[n],state.reserve[n]);state.ammo[n]+=k;state.reserve[n]-=k;state.reloading=0;updateHUD();}}
 let mx=input.moveX+(input.keys.has('KeyD')?1:0)-(input.keys.has('KeyA')?1:0),my=input.moveY+(input.keys.has('KeyW')?1:0)-(input.keys.has('KeyS')?1:0);let len=Math.hypot(mx,my);if(len>1){mx/=len;my/=len;}const speed=input.keys.has('ShiftLeft')?5.4:4.25;moveCircle(state.player,(mx*Math.cos(state.yaw)-my*Math.sin(state.yaw))*speed*dt,(-mx*Math.sin(state.yaw)-my*Math.cos(state.yaw))*speed*dt,.34,colliders);
 state.move+=Math.min(1,len)*dt*9;camera.position.set(state.player.x,1.65+Math.sin(state.move)*.028*Math.min(1,len),state.player.z);camera.rotation.set(state.pitch+state.recoil*.65,state.yaw,0,'YXZ');camera.updateMatrixWorld();
 if(input.fire)shoot();
 const gun=gunModels[state.weapon];gun.position.set(.24+Math.sin(state.move)*.008,-.23+Math.cos(state.move*2)*.006-state.reloading*.06,-.59+state.recoil);gun.rotation.x=state.reloading?-.38*Math.sin(Math.PI*state.reloading/state.reloadDuration):state.recoil*1.8;gun.rotation.z=state.reloading?.22:Math.sin(state.move)*.003;
 gun.updateMatrixWorld();flash.visible=state.flash>0;flash.position.copy(gun.userData.muzzle).applyMatrix4(gun.matrixWorld);flash.material.rotation=Math.random()*6;flash.scale.setScalar(state.weapon?.22:.14);muzzleLight.position.copy(flash.position);muzzleLight.intensity=state.flash>0?1.8:0;
 if(state.spawnLeft>0){state.spawnTimer-=dt;if(state.spawnTimer<=0&&enemies.length<6){spawn();state.spawnTimer=1.3;updateHUD();}}
 navTimer-=dt;if(navTimer<=0){nav=flowField(state.player,colliders);navTimer=.65;}
 for(const e of enemies){const distance=Math.hypot(state.player.x-e.x,state.player.z-e.z),sees=clearLine(e.x,e.z,state.player.x,state.player.z,colliders);e.phase+=dt*5;
 if(distance>6||!sees){const target=sees?state.player:nav(e);let dx=target.x-e.x,dz=target.z-e.z,l=Math.hypot(dx,dz);if(l>.05){dx/=l;dz/=l;const p={x:e.x,z:e.z};for(const other of enemies)if(other!==e){const a=e.x-other.x,b=e.z-other.z,d=Math.hypot(a,b);if(d<1.2&&d>.01){dx+=a/d*(1.2-d);dz+=b/d*(1.2-d);}}moveCircle(p,dx*e.speed*dt,dz*e.speed*dt,.42,colliders);e.x=p.x;e.z=p.z;}e.legs[0].rotation.x=Math.sin(e.phase)*.45;e.legs[1].rotation.x=-Math.sin(e.phase)*.45;}
 else e.legs.forEach(l=>l.rotation.x=0);
 e.mesh.position.set(e.x,0,e.z);e.mesh.rotation.y=Math.atan2(state.player.x-e.x,state.player.z-e.z);e.shoot-=dt;
 if(sees&&distance<29&&e.shoot<=0){e.shoot=2.4-state.wave*.16+Math.random()*.7;const m=new THREE.Mesh(tracerGeo,fxMat);m.position.set(e.x,1.25,e.z);let aim=new THREE.Vector3(state.player.x+(Math.random()-.5)*1.2,1.5+(Math.random()-.5)*.3,state.player.z+(Math.random()-.5)*1.2).sub(m.position).normalize();m.quaternion.setFromUnitVectors(new THREE.Vector3(0,0,1),aim);scene.add(m);projectiles.push({mesh:m,v:aim.multiplyScalar(12+state.wave),life:4});}
 }
 scene.updateMatrixWorld();
 for(let i=projectiles.length-1;i>=0;i--){const p=projectiles[i],prev=p.mesh.position.clone();p.mesh.position.addScaledVector(p.v,dt);p.life-=dt;const next=p.mesh.position;if(!clearLine(prev.x,prev.z,next.x,next.z,colliders)||Math.abs(next.x)>24||Math.abs(next.z)>24)p.life=0;else{const from=new THREE.Vector3(state.player.x,1.4,state.player.z),seg=next.clone().sub(prev),t=clamp(from.clone().sub(prev).dot(seg)/seg.lengthSq(),0,1);if(prev.addScaledVector(seg,t).distanceTo(from)<.48){hurt(8+state.wave*2);p.life=0;}}if(p.life<=0){scene.remove(p.mesh);projectiles.splice(i,1);}}
 for(let i=particles.length-1;i>=0;i--){const p=particles[i];p.mesh.position.addScaledVector(p.v,dt);p.v.y-=7*dt;p.life-=dt;p.mesh.scale.multiplyScalar(Math.max(0,1-dt*2));if(p.life<=0){scene.remove(p.mesh);particles.splice(i,1);}}
 for(let i=pickups.length-1;i>=0;i--){const p=pickups[i];p.mesh.rotation.y+=dt;p.mesh.position.y=.3+Math.sin(state.time*3)*.06;if(state.player.hp<100&&Math.hypot(p.x-state.player.x,p.z-state.player.z)<1.25){state.player.hp=Math.min(100,state.player.hp+30);scene.remove(p.mesh);pickups.splice(i,1);sound('reload');feed.unshift({text:'АПТЕЧКА +30 HP',life:3});updateHUD();}}
 if(state.mode==='playing'&&state.spawnLeft===0&&enemies.length===0){if(state.wave>=5){finish(true);return;}if(state.between===0){state.between=5;state.player.hp=Math.min(100,state.player.hp+25);state.reserve[0]+=60;state.reserve[1]+=12;announce('ПЕРИМЕТР ЗАЧИЩЕН','ПЕРЕДЫШКА','+25 здоровья · боезапас пополнен',4);updateHUD();}else{state.between-=dt;if(state.between<=0)startWave();}}
 if(state.announcement>0){state.announcement-=dt;if(state.announcement<=0)hide('announcement');}
 feed.forEach(f=>f.life-=dt);feed=feed.filter(f=>f.life>0);$('killFeed').textContent=feed.map(f=>f.text).join('\n');$('killFeed').style.whiteSpace='pre-line';$('damage').style.opacity=state.damage*.55;$('hitmarker').style.opacity=state.hit>0?1:0;
}
function resize(){renderer.setPixelRatio(Math.min(devicePixelRatio,prefs.quality==='high'?1.8:prefs.quality==='low'?.85:1.25));renderer.setSize(innerWidth,innerHeight,false);renderer.shadowMap.enabled=prefs.quality!=='low';const resolution=prefs.quality==='high'?2048:1024;if(sun.shadow.mapSize.x!==resolution){sun.shadow.map?.dispose();sun.shadow.map=null;sun.shadow.mapSize.set(resolution,resolution);}sun.shadow.needsUpdate=true;camera.aspect=gunCamera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();gunCamera.updateProjectionMatrix();if(innerHeight>innerWidth&&touch)pause();}
addEventListener('resize',resize);resize();
let last=performance.now(),shadowTime=0;function frame(now){requestAnimationFrame(frame);const dt=Math.min((now-last)/1000,.05);last=now;
 if(state.mode==='playing'){update(dt);}else if(state.mode==='menu'){camera.position.set(Math.sin(now*.00004)*5,3,20);camera.lookAt(0,2,-12);}
 dust.rotation.y+=dt*.006;if(now-shadowTime>(prefs.quality==='high'?25:66)){sun.shadow.needsUpdate=true;shadowTime=now;}renderer.info.reset();renderer.autoClear=true;renderer.render(scene,camera);
 if(state.mode==='playing'||state.mode==='paused'){renderer.autoClear=false;renderer.clearDepth();renderer.render(gunScene,gunCamera);}
}requestAnimationFrame(frame);

// UI actions all stay inside the offline game.
$('start').onclick=startGame;$('playAgain').onclick=startGame;$('pauseBtn').onclick=pause;$('resume').onclick=resume;$('restart').onclick=startGame;$('quit').onclick=menu;$('resultMenu').onclick=menu;
function syncSettings(){$('sensitivity').value=prefs.sensitivity;$('senseValue').value=prefs.sensitivity;$('volume').value=prefs.volume;$('volumeValue').value=prefs.volume;$('quality').value=prefs.quality;$('soundQuick').querySelector('b').textContent=prefs.volume?'ВКЛ':'ВЫКЛ';$('soundQuick').setAttribute('aria-label',prefs.volume?'Выключить звук':'Включить звук');}
$('settingsBtn').onclick=()=>{syncSettings();show('settings');};$('settingsClose').onclick=()=>{hide('settings');save();resize();};
$('sensitivity').oninput=e=>{prefs.sensitivity=+e.target.value;$('senseValue').value=prefs.sensitivity;};$('volume').oninput=e=>{prefs.volume=+e.target.value;syncSettings();};$('quality').onchange=e=>{prefs.quality=e.target.value;};
$('soundQuick').onclick=()=>{prefs.volume=prefs.volume?0:65;syncSettings();save();};
$('arsenalBtn').onclick=()=>show('arsenal');$('arsenalClose').onclick=()=>{hide('arsenal');save();};document.querySelectorAll('[data-weapon]').forEach(b=>{b.classList.toggle('selected',+b.dataset.weapon===prefs.firstWeapon);b.onclick=()=>{prefs.firstWeapon=+b.dataset.weapon;document.querySelectorAll('[data-weapon]').forEach(c=>c.classList.toggle('selected',c===b));};});
$('reload').onclick=reload;$('swap').onclick=()=>{if(state.mode==='playing')selectWeapon(1-state.weapon);};
function look(dx,dy){if(state.mode!=='playing')return;const sensitivity=.0006+prefs.sensitivity*.000027;state.yaw-=dx*sensitivity;state.pitch=clamp(state.pitch-dy*sensitivity,-1.05,1.05);}
addEventListener('keydown',e=>{if(['KeyW','KeyA','KeyS','KeyD','Space','ArrowUp','ArrowDown'].includes(e.code))e.preventDefault();if(e.repeat)return;if(e.code==='Escape'){state.mode==='playing'?pause():state.mode==='paused'?resume():null;return;}if(state.mode!=='playing')return;input.keys.add(e.code);if(e.code==='KeyR')reload();if(e.code==='KeyQ')selectWeapon(1-state.weapon);if(e.code==='Digit1')selectWeapon(0);if(e.code==='Digit2')selectWeapon(1);});
addEventListener('keyup',e=>input.keys.delete(e.code));
$('scene').addEventListener('pointerdown',e=>{if(state.mode==='playing'&&!touch&&e.button===0){if(document.pointerLockElement){input.fire=true;shoot();}else lockMouse();}});
addEventListener('pointerup',e=>{if(e.pointerType==='mouse')input.fire=false;});
addEventListener('mousemove',e=>{if(document.pointerLockElement)look(e.movementX,e.movementY);});
document.addEventListener('pointerlockchange',()=>{if(!document.pointerLockElement&&!touch&&state.mode==='playing')pause();});
addEventListener('blur',()=>{resetInput();pause();});document.addEventListener('visibilitychange',()=>{if(document.hidden)pause();});
function joystick(e){const r=$('joystick').getBoundingClientRect(),max=r.width*.32;let dx=e.clientX-r.left-r.width/2,dy=e.clientY-r.top-r.height/2,len=Math.hypot(dx,dy);if(len>max){dx*=max/len;dy*=max/len;}input.moveX=dx/max;input.moveY=-dy/max;$('stick').style.transform=`translate(${dx}px,${dy}px)`;}
$('joystick').onpointerdown=e=>{if(state.mode!=='playing'||input.joyId!==null)return;input.joyId=e.pointerId;$('joystick').setPointerCapture(e.pointerId);joystick(e);};$('joystick').onpointermove=e=>{if(e.pointerId===input.joyId)joystick(e);};
for(const event of ['pointerup','pointercancel','lostpointercapture'])$('joystick').addEventListener(event,e=>{if(e.pointerId===input.joyId){input.joyId=null;input.moveX=input.moveY=0;$('stick').style.transform='';}});
for(const id of ['lookZone','fire']){const el=$(id);el.onpointerdown=e=>{if(state.mode!=='playing')return;if(id==='fire'){input.fireId=e.pointerId;input.fire=true;shoot();}if(input.lookId===null){input.lookId=e.pointerId;input.lastX=e.clientX;input.lastY=e.clientY;}el.setPointerCapture(e.pointerId);e.preventDefault();};el.onpointermove=e=>{if(e.pointerId===input.lookId){look(e.clientX-input.lastX,e.clientY-input.lastY);input.lastX=e.clientX;input.lastY=e.clientY;}};for(const event of ['pointerup','pointercancel','lostpointercapture'])el.addEventListener(event,e=>{if(e.pointerId===input.lookId)input.lookId=null;if(e.pointerId===input.fireId){input.fireId=null;input.fire=false;}});}
addEventListener('contextmenu',e=>e.preventDefault());$('scene').addEventListener('webglcontextlost',e=>{e.preventDefault();pause();$('errorText').textContent='Графический контекст потерян. Закройте и откройте VoidX снова.';show('error');});
window.VoidX={pause,back(){if(!document.querySelector('#settings.hidden')){hide('settings');save();resize();}else if(!document.querySelector('#arsenal.hidden'))hide('arsenal');else if(state.mode==='playing')pause();else if(state.mode==='paused')resume();else menu();},snapshot(){return {mode:state.mode,hp:state.player.hp,player:{...state.player},yaw:state.yaw,pitch:state.pitch,wave:state.wave,score:state.score,kills:state.kills,ammo:[...state.ammo],reserve:[...state.reserve],weapon:state.weapon,reloading:state.reloading,time:state.time,enemyPositions:enemies.map(e=>({x:e.x,z:e.z,hp:e.hp})),remaining:enemies.length+state.spawnLeft,drawCalls:renderer.info.render.calls,triangles:renderer.info.render.triangles,quality:prefs.quality,shadows:renderer.shadowMap.enabled,shadowMapSize:sun.shadow.mapSize.x};}};
syncSettings();menu();selectWeapon(prefs.firstWeapon);
if(typeof __VOIDX_TEST__!=='undefined'&&__VOIDX_TEST__){window.__voidxTest={state,update,startGame,clearActors,makeEnemy,shoot,reload,selectWeapon,pause,resume,hurt,dropMed,camera,scene,colliders,enemyList:()=>enemies};}
