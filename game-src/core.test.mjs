import assert from 'node:assert/strict';
import {circleBlocked,moveCircle,clearLine,flowField,WEAPONS} from './core.mjs';
const boxes=[{x:0,z:0,w:4,d:4}];
assert.equal(circleBlocked(0,0,.4,boxes),true);
assert.equal(circleBlocked(3,0,.4,boxes),false);
assert.equal(circleBlocked(24,0,.4,[]),true);
let p={x:-5,z:0};moveCircle(p,20,0,.4,boxes);assert.ok(p.x<=-2.39,'Movement must not tunnel through cover');
p={x:-2.6,z:-1};moveCircle(p,1,2,.4,boxes);assert.ok(p.z>.9,'Movement slides along walls');assert.ok(!circleBlocked(p.x,p.z,.4,boxes));
assert.equal(clearLine(-5,0,5,0,boxes),false);
assert.equal(clearLine(-5,3,5,3,boxes),true);
assert.equal(clearLine(0,-5,0,5,boxes),false);
assert.equal(clearLine(4,-5,4,5,boxes),true);
const flow=flowField({x:8,z:0},boxes);p={x:-8,z:0};
for(let i=0;i<700;i++){const t=flow(p),d=Math.hypot(t.x-p.x,t.z-p.z);if(d>.01)moveCircle(p,(t.x-p.x)/d*.12,(t.z-p.z)/d*.12,.42,boxes);}
assert.ok(Math.hypot(p.x-8,p.z)<2,'Enemy navigation must find route around cover');
assert.equal(WEAPONS[0].mag,30);assert.equal(WEAPONS[1].pellets,8);
console.log('PASS: collisions, anti-tunneling, wall sliding, line of sight, navigation around cover, weapon configuration.');
