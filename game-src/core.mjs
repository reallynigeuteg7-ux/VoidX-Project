export function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
export function circleBlocked(x, z, radius, boxes) {
  if (Math.abs(x) > 24 - radius || Math.abs(z) > 24 - radius) return true;
  return boxes.some(b => { const dx = x - clamp(x, b.x-b.w/2, b.x+b.w/2), dz = z-clamp(z,b.z-b.d/2,b.z+b.d/2); return dx*dx+dz*dz < radius*radius; });
}
export function moveCircle(pos, dx, dz, radius, boxes) {
  const steps = Math.max(1, Math.ceil(Math.hypot(dx,dz) / (radius*.5)));
  for(let i=0;i<steps;i++) {if(!circleBlocked(pos.x+dx/steps,pos.z,radius,boxes))pos.x+=dx/steps;if(!circleBlocked(pos.x,pos.z+dz/steps,radius,boxes))pos.z+=dz/steps;}
}
export function clearLine(x1,z1,x2,z2,boxes) {
  const dx=x2-x1,dz=z2-z1;
  for(const b of boxes) {
    let lo=0,hi=1;
    for(const [p,d,mn,mx] of [[x1,dx,b.x-b.w/2,b.x+b.w/2],[z1,dz,b.z-b.d/2,b.z+b.d/2]]) {
      if(Math.abs(d)<1e-8) {if(p<mn||p>mx){lo=2;break;}}
      else {let a=(mn-p)/d,c=(mx-p)/d;if(a>c)[a,c]=[c,a];lo=Math.max(lo,a);hi=Math.min(hi,c);}
    }
    if(lo<=hi&&hi>=0&&lo<=1)return false;
  }
  return true;
}
export function flowField(target, boxes) {
 const size=32,cell=1.5,origin=-24,dist=new Int16Array(size*size).fill(-1),free=new Uint8Array(size*size);
 const idx=(x,z)=>clamp(Math.floor((z-origin)/cell),0,size-1)*size+clamp(Math.floor((x-origin)/cell),0,size-1);
 for(let z=0;z<size;z++)for(let x=0;x<size;x++)free[z*size+x]=!circleBlocked(origin+(x+.5)*cell,origin+(z+.5)*cell,.48,boxes);
 let start=idx(target.x,target.z);if(!free[start]){let best=1e9;for(let i=0;i<free.length;i++)if(free[i]){let d=(origin+(i%size+.5)*cell-target.x)**2+(origin+(Math.floor(i/size)+.5)*cell-target.z)**2;if(d<best){best=d;start=i;}}}
 const queue=[start];dist[start]=0;
 for(let q=0;q<queue.length;q++){let i=queue[q],x=i%size,z=Math.floor(i/size);for(const [nx,nz] of [[x-1,z],[x+1,z],[x,z-1],[x,z+1]]){let ni=nz*size+nx;if(nx>=0&&nz>=0&&nx<size&&nz<size&&free[ni]&&dist[ni]<0){dist[ni]=dist[i]+1;queue.push(ni);}}}
 return (p)=>{let i=idx(p.x,p.z),best=i,d=dist[i]<0?9999:dist[i],x=i%size,z=Math.floor(i/size);for(const [nx,nz] of [[x-1,z],[x+1,z],[x,z-1],[x,z+1]]){if(nx<0||nz<0||nx>=size||nz>=size)continue;let ni=nz*size+nx;if(dist[ni]>=0&&dist[ni]<d){best=ni;d=dist[ni];}}return {x:origin+(best%size+.5)*cell,z:origin+(Math.floor(best/size)+.5)*cell};};
}
export const WEAPONS=[{name:'VX—01 / ШТУРМОВАЯ',mag:30,reserve:180,damage:28,pellets:1,interval:.1,reload:1.75,spread:.007},{name:'VX—08 / ДРОБОВИК',mag:8,reserve:48,damage:16,pellets:8,interval:.7,reload:2.3,spread:.055}];
