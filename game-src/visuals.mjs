import * as THREE from 'three';
import {RoundedBoxGeometry} from 'three/examples/jsm/geometries/RoundedBoxGeometry.js';
import {RoomEnvironment} from 'three/examples/jsm/environments/RoomEnvironment.js';

// Everything is generated locally: no texture downloads or external model requests.
let seed=7321;
const rand=()=>((seed=(1664525*seed+1013904223)>>>0)/4294967296);
function canvasTexture(draw,size=512){const c=document.createElement('canvas');c.width=c.height=size;draw(c.getContext('2d'),size);const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;t.wrapS=t.wrapT=THREE.RepeatWrapping;return t;}
function surface(kind){return canvasTexture((a,n)=>{
 const base=kind==='concrete'?142:kind==='asphalt'?77:180;a.fillStyle=`rgb(${base},${base},${base})`;a.fillRect(0,0,n,n);
 for(let i=0;i<75;i++){let x=rand()*n,y=rand()*n,r=12+rand()*100;const g=a.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,`rgba(${i%3?0:255},${i%3?0:255},${i%3?0:255},${.025+rand()*.08})`);g.addColorStop(1,'transparent');a.fillStyle=g;a.fillRect(x-r,y-r,r*2,r*2);}
 for(let i=0;i<35000;i++){let v=Math.round(base-35+rand()*70);a.fillStyle=`rgba(${v},${v},${v},${.15+rand()*.55})`;let s=rand()*1.5+.3;a.fillRect(rand()*n,rand()*n,s,s);}
 if(kind==='concrete'){for(let i=0;i<55;i++){let x=rand()*n,y=rand()*n;a.fillStyle='#00000017';a.beginPath();a.ellipse(x,y,rand()*2+.4,rand()*2+.4,0,0,7);a.fill();}a.strokeStyle='#55555565';a.lineWidth=2;a.strokeRect(1,1,n-2,n-2);a.strokeStyle='#eeeeee20';a.strokeRect(4,4,n-8,n-8);}
 if(kind==='asphalt'){for(let i=0;i<8;i++){let x=rand()*n,y=rand()*n;a.beginPath();a.moveTo(x,y);for(let j=0;j<12;j++){x+=(rand()-.5)*35;y+=rand()*20;a.lineTo(x,y);}a.strokeStyle='#22222290';a.lineWidth=.6+rand();a.stroke();}}
 if(kind==='metal'){for(let i=0;i<85;i++){const x=rand()*n,y=rand()*n;a.strokeStyle=i%3?'#dddddd15':'#00000028';a.lineWidth=.5;a.beginPath();a.moveTo(x,y);a.lineTo(x+rand()*65,y+rand()*2);a.stroke();}}
 });}
function worldMapped(material,scale=.48){material.onBeforeCompile=shader=>{
 shader.vertexShader='varying vec3 vxPosition; varying vec3 vxNormal;\n'+shader.vertexShader;
 shader.vertexShader=shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nvxPosition=(modelMatrix*vec4(position,1.0)).xyz;vxNormal=normalize(mat3(modelMatrix)*normal);');
 shader.fragmentShader='varying vec3 vxPosition; varying vec3 vxNormal;\n'+shader.fragmentShader;
 shader.fragmentShader=shader.fragmentShader.replace('#include <map_fragment>',`
 vec3 vxWeight=pow(abs(vxNormal),vec3(6.0));vxWeight/=max(dot(vxWeight,vec3(1.0)),0.001);
 vec3 vxP=vxPosition*${scale.toFixed(3)};
 vec3 vxSurface=texture2D(map,vxP.yz).rgb*vxWeight.x+texture2D(map,vxP.xz).rgb*vxWeight.y+texture2D(map,vxP.xy).rgb*vxWeight.z;
 diffuseColor.rgb*=vxSurface;
 float vxHeight=dot(vxSurface,vec3(0.333333));
 `);
 shader.fragmentShader=shader.fragmentShader.replace('#include <normal_fragment_maps>',`#ifdef USE_BUMPMAP
 normal=perturbNormalArb(-vViewPosition,normal,vec2(dFdx(vxHeight),dFdy(vxHeight))*bumpScale,faceDirection);
 #endif`);
 };material.customProgramCacheKey=()=>`voidx-triplanar-${scale}`;return material;}
export function visualMaterials(renderer){
 const concrete=surface('concrete'),asphalt=surface('asphalt'),metal=surface('metal');
 for(const t of [concrete,asphalt,metal])t.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());asphalt.repeat.set(18,18);
 return {
 concrete:worldMapped(new THREE.MeshStandardMaterial({color:0xb7b7b7,map:concrete,bumpMap:concrete,bumpScale:.11,roughness:.94,envMapIntensity:.15})),
 dark:new THREE.MeshStandardMaterial({color:0x252525,map:metal,roughness:.72,metalness:.22,envMapIntensity:.32}),
 metal:new THREE.MeshStandardMaterial({color:0x595959,map:metal,bumpMap:metal,bumpScale:.008,metalness:.85,roughness:.38,envMapIntensity:.6}),
 light:new THREE.MeshStandardMaterial({color:0xb0b0b0,metalness:.5,roughness:.4,envMapIntensity:.6}),
 white:new THREE.MeshStandardMaterial({color:0xffffff,emissive:0xffffff,emissiveIntensity:1.2,roughness:.3}),
 black:new THREE.MeshStandardMaterial({color:0x121212,roughness:.88,metalness:.02}),
 cloth:new THREE.MeshStandardMaterial({color:0x707070,map:concrete,bumpMap:concrete,bumpScale:.004,roughness:1}),
 gun:new THREE.MeshStandardMaterial({color:0x747474,map:metal,bumpMap:metal,bumpScale:.006,metalness:.75,roughness:.34,envMapIntensity:1.1}),
 floor:new THREE.MeshStandardMaterial({color:0xb0b0b0,map:asphalt,bumpMap:asphalt,bumpScale:.065,roughness:.83,metalness:.05,envMapIntensity:.25}),
 };
}
export function lighting(renderer,scene,gunScene,sun){
 renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.02;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
 const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment();const env=pmrem.fromScene(room,.06).texture;scene.environment=gunScene.environment=env;room.dispose();pmrem.dispose();
 sun.castShadow=true;sun.position.set(-18,30,13);sun.intensity=3.4;sun.shadow.camera.left=-32;sun.shadow.camera.right=32;sun.shadow.camera.top=32;sun.shadow.camera.bottom=-32;sun.shadow.camera.near=1;sun.shadow.camera.far=95;sun.shadow.bias=-.00016;sun.shadow.normalBias=.055;sun.shadow.autoUpdate=false;
 const sky=new THREE.Mesh(new THREE.SphereGeometry(130,32,16),new THREE.ShaderMaterial({side:THREE.BackSide,depthWrite:false,vertexShader:`varying vec3 vSky;void main(){vSky=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}`,fragmentShader:`
 varying vec3 vSky;
 float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
 float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
 void main(){vec3 d=normalize(vSky);vec2 p=d.xz/(max(d.y,.04)+.4)*2.8;float n=noise(p)*.55+noise(p*2.03)*.3+noise(p*4.1)*.15;float sky=mix(.6,.19,smoothstep(-.06,.9,d.y));sky+=smoothstep(.38,.77,n)*.2*smoothstep(.01,.3,d.y);gl_FragColor=vec4(vec3(sky),1.0);}
 `}));scene.add(sky);
}
function rounded(parent,x,y,z,w,h,d,material,radius=.025){const m=new THREE.Mesh(new RoundedBoxGeometry(w,h,d,1,Math.min(radius,w*.23,h*.23,d*.23)),material);m.position.set(x,y,z);m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
function cyl(parent,x,y,z,r,h,material,rx=0){const m=new THREE.Mesh(new THREE.CylinderGeometry(r,r,h,16),material);m.position.set(x,y,z);m.rotation.x=rx;m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
function limb(parent,a,b,r1,r2,material){const start=new THREE.Vector3(...a),end=new THREE.Vector3(...b),dir=end.clone().sub(start);const m=new THREE.Mesh(new THREE.CylinderGeometry(r2,r1,dir.length(),12),material);m.position.copy(start.add(end).multiplyScalar(.5));m.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),dir.normalize());m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
function ellipsoid(parent,x,y,z,rx,ry,rz,material){const m=new THREE.Mesh(new THREE.SphereGeometry(1,16,10),material);m.position.set(x,y,z);m.scale.set(rx,ry,rz);m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
function label(parent,text,x,y,z,w,h){const texture=canvasTexture((a,n)=>{a.clearRect(0,0,n,n);a.fillStyle='#c5c5c5';a.font='bold 70px monospace';a.textAlign='center';a.fillText(text,n/2,n*.57);},256);texture.wrapS=texture.wrapT=THREE.ClampToEdgeWrapping;const m=new THREE.Mesh(new THREE.PlaneGeometry(w,h),new THREE.MeshBasicMaterial({map:texture,transparent:true,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-1}));m.position.set(x,y,z);parent.add(m);return m;}

export function weapon(parent,type,m){const g=new THREE.Group();g.position.set(.24,-.23,-.59);parent.add(g);
 rounded(g,0,0,-.055,.116,.128,.4,m.gun,.018);rounded(g,0,.072,-.08,.108,.065,.38,m.gun,.014);
 rounded(g,0,-.005,.16,.087,.11,.1,m.dark);rounded(g,0,-.052,.26,.115,.195,.18,m.black,.022);
 const grip=rounded(g,0,-.135,.076,.085,.19,.086,m.black,.019);grip.rotation.x=-.26;
 const mag=rounded(g,0,-.175,-.083,.087,.235,.127,m.gun,.016);mag.rotation.x=-.15;
 for(let k=0;k<5;k++)rounded(g,.047,-.11-k*.036,-.083,.008,.014,.089,m.dark,.003);
 rounded(g,0,.023,-.375,type?.148:.128,.125,.28,m.gun,.017);
 for(let k=0;k<6;k++){rounded(g,.067,.027,-.28-k*.038,.006,.046,.019,m.black,.003);rounded(g,-.067,.027,-.28-k*.038,.006,.046,.019,m.black,.003);rounded(g,0,.115,-.38+k*.047,.139,.015,.018,m.metal,.003);}
 cyl(g,0,.032,-.61,type?.028:.018,.25,m.metal,Math.PI/2);cyl(g,0,.032,-.756,type?.036:.028,.07,m.gun,Math.PI/2);
 for(let i=0;i<3;i++)rounded(g,0,.058,-.743-i*.016,.038,.009,.008,m.black,.002);
 if(type){cyl(g,0,-.047,-.55,.025,.35,m.gun,Math.PI/2);for(let i=0;i<6;i++)rounded(g,0,-.04,-.31-i*.027,.159,.087,.012,m.black,.002);}
 // Open reflex sight and fine front post.
 rounded(g,0,.13,-.065,.11,.035,.12,m.metal,.006);
 for(const s of [-1,1])rounded(g,s*.047,.176,-.075,.015,.09,.034,m.gun,.006);
 rounded(g,0,.218,-.075,.11,.017,.034,m.gun,.006);rounded(g,0,.127,-.075,.018,.026,.016,m.light,.004);
 rounded(g,0,.104,-.61,.07,.027,.035,m.gun,.004);rounded(g,0,.142,-.61,.012,.06,.019,m.metal,.003);
 rounded(g,.06,.015,-.078,.005,.044,.118,m.black,.002);rounded(g,.067,.013,-.078,.009,.017,.06,m.metal,.003);
 for(const z of [-.2,.08]){const bolt=cyl(g,.063,-.012,z,.011,.012,m.light);bolt.rotation.z=Math.PI/2;}
 rounded(g,.066,-.044,.03,.012,.013,.043,m.light,.003);
 // Curved trigger guard, rubber grip lines and padded gloves.
 const guard=new THREE.Mesh(new THREE.TorusGeometry(.048,.007,6,14,Math.PI*1.65),m.gun);guard.position.set(0,-.11,.002);guard.rotation.y=Math.PI/2;g.add(guard);
 for(let i=0;i<5;i++)rounded(g,0,-.075-i*.023,.113,.08,.007,.006,m.dark,.002);
 ellipsoid(g,.034,-.17,.085,.067,.092,.08,m.black);ellipsoid(g,-.039,-.077,-.36,.07,.059,.086,m.black);
 limb(g,[.032,-.17,.11],[.16,-.36,.31],.065,.085,m.cloth);limb(g,[-.038,-.08,-.33],[-.17,-.29,-.05],.065,.09,m.cloth);limb(g,[-.17,-.29,-.05],[-.13,-.38,.3],.087,.1,m.cloth);
 for(let i=0;i<4;i++){ellipsoid(g,-.075+i*.029,-.117,-.4,.016,.025,.043,m.dark);ellipsoid(g,.075,-.14-i*.026,.073,.028,.018,.034,m.dark);}
 label(g,'VX—08'.replace('08',type?'08':'01'),0,.023,.166,.075,.027);
 g.userData.muzzle=new THREE.Vector3(0,.032,-.803);return g;
}

export function operative(g,e,m){
 rounded(g,0,1.17,0,.48,.61,.29,m.cloth,.09);rounded(g,0,1.28,.17,.42,.44,.11,m.gun,.04);
 rounded(g,0,1.38,-.2,.36,.43,.15,m.dark,.05);rounded(g,0,.86,.015,.41,.2,.3,m.dark,.05);
 for(const x of [-.14,0,.14])rounded(g,x,1.1,.24,.115,.2,.1,m.dark,.017);
 limb(g,[-.19,1.47,.18],[-.16,.92,.2],.023,.023,m.light);limb(g,[.19,1.47,.18],[.16,.92,.2],.023,.023,m.dark);
 cyl(g,0,1.58,0,.095,.12,m.black);
 const head=new THREE.Group();e.head=head;head.position.set(0,1.78,0);g.add(head);ellipsoid(head,0,0,0,.195,.22,.19,m.gun);rounded(head,0,.032,.169,.3,.087,.046,m.black,.025);rounded(head,0,.04,.195,.235,.027,.012,m.light,.003);rounded(head,0,-.095,.156,.2,.096,.065,m.dark,.025);
 for(const side of [-1,1]){rounded(head,side*.181,0,.006,.058,.13,.1,m.dark,.025);rounded(g,side*.285,1.4,.016,.18,.19,.23,m.gun,.045);limb(g,[side*.32,1.35,.03],[side*.33,1.11,.13],.087,.071,m.cloth);limb(g,[side*.33,1.11,.13],[side*.2,1.17,.43],.075,.055,m.cloth);ellipsoid(g,side*.2,1.16,.43,.063,.071,.085,m.black);}
 head.traverse(part=>{if(part.isMesh)part.userData.head=true;});
 e.legs=[];for(const s of [-1,1]){const leg=new THREE.Group();leg.position.set(s*.145,.82,0);g.add(leg);limb(leg,[0,0,0],[0,-.36,.006],.108,.086,m.cloth);limb(leg,[0,-.38,.006],[0,-.67,0],.075,.072,m.cloth);rounded(leg,0,-.37,.09,.158,.15,.095,m.gun,.034);rounded(leg,0,-.7,.058,.19,.15,.31,m.black,.043);e.legs.push(leg);}
 rounded(g,.19,1.18,.4,.1,.105,.49,m.gun,.015);cyl(g,.19,1.2,.72,.019,.2,m.metal,Math.PI/2);rounded(g,.19,1.12,.43,.077,.17,.1,m.black,.013);
}

export function radialTexture(){return canvasTexture((a,n)=>{const g=a.createRadialGradient(n/2,n/2,0,n/2,n/2,n/2);g.addColorStop(0,'#ffffff');g.addColorStop(.1,'#ffffffb0');g.addColorStop(.45,'#ffffff30');g.addColorStop(1,'#ffffff00');a.fillStyle=g;a.fillRect(0,0,n,n);},64);}
export function dressArena(scene,m,box,cylinder,colliders){
 const contact=canvasTexture((a,n)=>{const g=a.createRadialGradient(n/2,n/2,n*.13,n/2,n/2,n*.5);g.addColorStop(0,'#000000b0');g.addColorStop(.5,'#00000060');g.addColorStop(1,'#00000000');a.fillStyle=g;a.fillRect(0,0,n,n);},128);
 const contactMat=new THREE.MeshBasicMaterial({map:contact,transparent:true,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-1});
 for(const b of colliders){if(b.w>30||b.d>30)continue;const p=new THREE.Mesh(new THREE.PlaneGeometry(b.w+2.5,b.d+2.5),contactMat);p.rotation.x=-Math.PI/2;p.position.set(b.x,.005,b.z);scene.add(p);}
 const stripe=canvasTexture((a,n)=>{a.fillStyle='#bcbcbc';a.fillRect(0,0,n,n);a.fillStyle='#292929';for(let x=-n;x<2*n;x+=n/4){a.beginPath();a.moveTo(x,0);a.lineTo(x+n*.11,0);a.lineTo(x-n*.89,n);a.lineTo(x-n,n);a.fill();}for(let i=0;i<1100;i++){a.fillStyle=i%2?'#b0b0b050':'#14141440';a.fillRect(rand()*n,rand()*n,rand()*14,2);}},256);stripe.repeat.set(5,1);
 const stripeMat=new THREE.MeshStandardMaterial({map:stripe,roughness:.85,color:0xc0c0c0});
 for(const b of colliders){if(b.w<3||b.w>10||b.d>10)continue;box(scene,b.x,1.85,b.z+b.d/2+.021,b.w-.3,.2,.03,stripeMat);for(const s of [-1,1]){box(scene,b.x+s*(b.w/2-.14),1.1,b.z+b.d/2+.034,.13,1.6,.025,m.metal);for(let y=.45;y<1.8;y+=.55)box(scene,b.x+s*(b.w/2-.14),y,b.z+b.d/2+.054,.04,.04,.016,m.light);}}
 // Paneled facades, weathered shutters, conduits and ducts.
 for(const s of [-1,1])for(let z=-20;z<24;z+=4){box(scene,s*23.91,3.35,z,.06,.055,3.7,m.dark);box(scene,s*23.9,1.55,z,.08,2.9,.035,m.dark);if(z%8===0){box(scene,s*23.77,3.9,z,.4,.55,1.2,m.metal);for(let j=0;j<6;j++)box(scene,s*23.53,3.7+j*.065,z,.02,.023,1,m.black);}}
 for(const s of [-1,1]){cylinder(scene,s*23.56,3.8,0,.12,45,m.metal,Math.PI/2);for(let z=-21;z<24;z+=6)box(scene,s*23.47,3.8,z,.15,.35,.07,m.dark);}
 box(scene,0,2.25,-23.72,7.2,4.5,.13,m.metal);for(let y=.22;y<4.5;y+=.19)box(scene,0,y,-23.62,7,.036,.04,m.dark);
 for(const x of [-3.75,3.75])box(scene,x,2.4,-23.45,.25,4.8,.46,m.dark);box(scene,0,4.8,-23.5,7.8,.24,.5,m.dark);
 for(const x of [-17,17]){box(scene,x,5.2,-23.45,4.2,.2,.7,m.dark);for(let y=.7;y<4.7;y+=.34)box(scene,x,y,-23.56,2.5,.075,.07,m.metal);for(const dx of [-1.35,1.35])box(scene,x+dx,2.7,-23.52,.09,4.6,.1,m.metal);}
 // Concrete repairs near the viewer create a readable sense of scale.
 for(let i=0;i<35;i++){const x=(rand()-.5)*45,z=(rand()-.5)*44;if(Math.abs(x)<3)continue;const stone=rounded(scene,x,.03+rand()*.07,z,.08+rand()*.32,.08+rand()*.12,.1+rand()*.35,m.concrete,.03);stone.rotation.y=rand()*6;}
 const puddleMat=new THREE.MeshStandardMaterial({color:0x313131,metalness:.58,roughness:.17,envMapIntensity:.85,transparent:true,opacity:.68,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-1});
 for(const [x,z,r] of [[-4,15,2.2],[6,3,1.8],[-5,-8,2.7],[13,17,1.2],[2,-17,2]]){const shape=new THREE.Shape();for(let j=0;j<=32;j++){let a=j/32*Math.PI*2,d=r*(.8+Math.sin(a*5)*.09+Math.cos(a*3)*.11);let px=Math.cos(a)*d,py=Math.sin(a)*d*.48;j?shape.lineTo(px,py):shape.moveTo(px,py);}const p=new THREE.Mesh(new THREE.ShapeGeometry(shape),puddleMat);p.rotation.x=-Math.PI/2;p.position.set(x,.013,z);scene.add(p);}
 // A steel bridge above the rear courtyard and sagging utility cables.
 for(const z of [-18,-20]){box(scene,0,10.7,z,48,.28,.24,m.metal);box(scene,0,12.3,z,48,.18,.2,m.metal);for(let x=-22;x<23;x+=4){box(scene,x,11.5,z,.16,1.6,.18,m.metal);const beam=box(scene,x+1,11.5,z,2.6,.1,.12,m.dark);beam.rotation.z=.6;}}
 for(let x=-22;x<24;x+=2)box(scene,x,10.59,-19,.08,.06,2.3,m.dark);
 const cableMat=new THREE.MeshStandardMaterial({color:0x151515,roughness:.85});for(const z of [-8,0,8]){const curve=new THREE.CatmullRomCurve3([new THREE.Vector3(-24,10,z),new THREE.Vector3(0,7.8,z-1),new THREE.Vector3(24,11,z-2)]);const cable=new THREE.Mesh(new THREE.TubeGeometry(curve,22,.025,4,false),cableMat);scene.add(cable);}
 // Readable architectural layers on the far skyline.
 for(const x of [-17,14]){box(scene,x,13,-33,8,9,7,m.concrete);for(let y=10;y<18;y+=2)for(let px=x-3;px<x+4;px+=1.5)box(scene,px,y,-29.45,.8,1.2,.06,m.black);cylinder(scene,x+2,23,-33,.48,12,m.metal);for(let y=18;y<29;y+=2)cylinder(scene,x+2,y,-33,.57,.16,m.dark);}
 const stencil=label(scene,'SECTOR  /  07',0,8.6,-23.77,7,1.2);stencil.material.opacity=.7;
}
