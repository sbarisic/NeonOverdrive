// Procedural audio, unlocked by a gesture. No network assets or audio downloads.
export function createArenaAudio() {
 let context, muted=false, previous=null, lastShot=0, voices=0, hums=new Map(), humTimeout;
 function stopHum(){clearTimeout(humTimeout);for(const hum of hums.values()){hum.oscillator.stop();hum.oscillator.disconnect();hum.gain.disconnect();}hums.clear();}
 function laser(weapons){
   if(!weapons.length||muted||!context||context.state!=='running'||document.hidden){stopHum();return;}
   const active=new Set(weapons);
   for(const [id,hum] of hums)if(!active.has(id)){hum.oscillator.stop();hum.oscillator.disconnect();hum.gain.disconnect();hums.delete(id);}
   for(const id of active)if(!hums.has(id)){const oscillator=context.createOscillator(),gain=context.createGain();oscillator.type='sine';oscillator.frequency.value=145+(id||0)*35;oscillator.connect(gain);gain.connect(context.destination);oscillator.start();hums.set(id,{oscillator,gain});}
   for(const hum of hums.values())hum.gain.gain.value=.035/Math.sqrt(hums.size);
   clearTimeout(humTimeout);humTimeout=setTimeout(stopHum,300);
 }
 document.addEventListener('visibilitychange',()=>{if(document.hidden)stopHum();});
 const unlock=()=>{try{context ||= new (window.AudioContext||window.webkitAudioContext)();if(context.state==='suspended')context.resume().catch(()=>{});}catch{}};
 window.addEventListener('pointerdown',unlock);window.addEventListener('keydown',unlock);
 function tone(start,end,duration=.1,type='sine',volume=.05,delay=0){
   if(muted||!context||context.state!=='running'||document.hidden||voices>=20)return;
   const oscillator=context.createOscillator(),gain=context.createGain(),at=context.currentTime+delay;
   oscillator.type=type;oscillator.frequency.setValueAtTime(start,at);oscillator.frequency.exponentialRampToValueAtTime(Math.max(20,end),at+duration);
   gain.gain.setValueAtTime(0,at);gain.gain.linearRampToValueAtTime(volume,at+.005);gain.gain.exponentialRampToValueAtTime(.0001,at+duration);
   oscillator.connect(gain);gain.connect(context.destination);voices++;
   oscillator.onended=()=>{oscillator.disconnect();gain.disconnect();voices--;};oscillator.start(at);oscillator.stop(at+duration+.02);
 }
 return {setMuted(value){muted=value;if(value)stopHum();},reset(){previous=null;lastShot=0;stopHum();},
   observe(frame,id){const self=frame.players.find(p=>p.id===id);
     laser(self&&self.hull>0&&!self.editing?[...new Set((frame.beams||[]).filter(b=>b.owner===id).map(b=>b.weapon||0))]:[]);
     if(previous&&frame.tick>previous.tick&&frame.tick-previous.tick<30){
       const old=previous.players.find(p=>p.id===id),oldShots=new Set(previous.shots.map(s=>s.id));
       const shot=frame.shots.find(s=>s.owner===id&&!s.parent&&!oldShots.has(s.id));
       if(shot&&frame.tick-lastShot>=2){lastShot=frame.tick;
         if(shot.kind===2)tone(1600,300,.12,'sine',.04);
         else if(shot.kind===3)tone(160,35,.24,'sawtooth',.035);
         else if(shot.kind===4)tone(320,65,.2,'triangle',.06);
         else tone(780,170,.07,'square',.022);
       }
       if(self&&old&&self.life===old.life){
         if(self.hull<old.hull||self.shield<old.shield)tone(140,35,.18,'sawtooth',.04);
         if(self.hull<=0&&old.hull>0)tone(220,25,.5,'sawtooth',.055);
         if(self.experience>old.experience)tone(880,1320,.1,'sine',.035);
         if(self.level>old.level)for(let i=0;i<4;i++)tone(440*2**(i/3),440*2**(i/3),.18,'sine',.045,i*.09);
         if(JSON.stringify(self.skills)!==JSON.stringify(old.skills)||JSON.stringify(self.weapons)!==JSON.stringify(old.weapons))tone(550,1100,.16,'triangle',.045);
       }
       const newExplosions=(frame.explosions||[]).filter(e=>!(previous.explosions||[]).some(old=>old.id===e.id));
       if(self&&newExplosions.some(e=>Math.hypot(e.x-self.x,e.y-self.y)<800))tone(110,22,.28,'sawtooth',.045);
       const enemies=new Map((frame.enemies||[]).map(e=>[e.id,e]));
       if(self&&(previous.enemies||[]).some(e=>Math.hypot(e.x-self.x,e.y-self.y)<700&&(!enemies.has(e.id)||enemies.get(e.id).hull<e.hull)))tone(240,55,.09,'triangle',.045);
     }previous=frame;
   }
 };
}
