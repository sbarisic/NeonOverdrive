const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
let started=0,stopped=0;const listeners={},gains=[];
class AudioContext{
 constructor(){this.state='running';this.currentTime=0;this.destination={};}
 createOscillator(){return {type:'sine',frequency:{value:0,setValueAtTime(){},exponentialRampToValueAtTime(){}},connect(){},disconnect(){},start(){started++;},stop(){stopped++;}};}
 createGain(){const gain={gain:{value:0,setValueAtTime(){},linearRampToValueAtTime(){},exponentialRampToValueAtTime(){}},connect(){},disconnect(){}};gains.push(gain);return gain;}
}
const sandbox={window:{AudioContext,addEventListener(type,fn){listeners[type]=fn;}},document:{hidden:false,addEventListener(){}},setTimeout(){return 1;},clearTimeout(){}};
vm.createContext(sandbox);vm.runInContext(fs.readFileSync('src/ArcadeDodger/Web/Web/audio.js','utf8').replace('export function','function')+'\nthis.audio=createArenaAudio();',sandbox);
listeners.pointerdown();
const self={id:1,life:0,hull:3,shield:2,skills:[],weapons:[{emitter:1,attachments:Array(8).fill(0),enabled:true}],level:1,experience:0,dashTicks:0};
const frame=(tick,shots=[],beams=[])=>({tick,players:[self],shots,beams,enemies:[]});
sandbox.audio.observe(frame(1),1);sandbox.audio.observe(frame(3,[{id:1,owner:1,kind:1,parent:0}]),1);assert.equal(started,1);
sandbox.audio.observe(frame(5,[{id:2,owner:1,kind:1,parent:1},{id:3,owner:1,kind:1,parent:1}]),1);assert.equal(started,1,'splitting must not play a second pew');
sandbox.audio.observe(frame(7,[],[{owner:1}]),1);assert.equal(started,2);
sandbox.audio.observe(frame(9,[],[{owner:1},{owner:1},{owner:1}]),1);assert.equal(started,2,'forked beam sustains one hum');
const before=stopped;sandbox.audio.observe(frame(11),1);assert.equal(stopped,before+1);
sandbox.audio.setMuted(true);sandbox.audio.observe(frame(13,[],[{owner:1}]),1);assert.equal(started,2);
sandbox.audio.setMuted(false);
sandbox.audio.observe(frame(15,[],[0,1,2,3].flatMap(weapon=>[{owner:1,weapon},{owner:1,weapon}])),1);
assert.equal(started,6,'four firing weapons each get one hum regardless of branches');
assert.ok(Math.abs(gains.slice(-4).reduce((sum,g)=>sum+g.gain.value**2,0)-.035**2)<1e-10,'combined hum power stays bounded');
const beforeRelease=stopped;sandbox.audio.observe(frame(17,[],[{owner:1,weapon:2}]),1);
assert.equal(stopped,beforeRelease+3,'stopping three weapons preserves the remaining hum');
assert.equal(started,6);sandbox.audio.reset();assert.equal(stopped,beforeRelease+4);
console.log('PASS: silent splits, one hum per firing weapon, bounded combined gain, selective release, reset and mute.');
