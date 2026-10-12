// Illustrations are presentation only; all skill rules arrive from the server catalog.
export const branches=['Defense','Strength','Fire Rate','Weapon Bays','Masteries','Mobility','Fortune','Collection'];
export const colors=['#7bded9','#f3a365','#efda82','#94bfff','#cf9dfa','#79d5ff','#f9d375','#9fe7a6'];
const art={
 shield:'<path d="M32 10 49 17 46 38 32 52 18 38 15 17Z"/><path d="M32 17V44M22 25H42"/>',
 recharge:'<path d="M42 15A20 20 0 1 0 51 37M42 7V20H55"/><path d="M34 19 24 34H34L29 46 42 29H32Z"/>',
 flux:'<path d="M16 21Q24 10 32 21T48 21M16 32Q24 21 32 32T48 32M16 43Q24 32 32 43T48 43"/>',
 siphon:'<path d="M32 12 47 18 44 38 32 50 20 38 17 18ZM8 33H26M21 27 27 33 21 39M56 33H38M43 27 37 33 43 39"/>',
 hull:'<path d="M15 17 32 10 49 17V46L32 54 15 46ZM15 17 32 28 49 17M32 28V54"/><path d="M22 22V41M42 22V41"/>',
 repair:'<path d="M17 14H47V50H17Z"/><path d="M27 22H37V28H43V38H37V44H27V38H21V28H27Z"/>',
 strength:'<path d="M16 49 43 14 51 21 24 56ZM20 35 36 48M36 12 39 5M51 31 59 30M27 18 22 12"/>',
 dynamics:'<path d="M12 43 39 16 48 16 48 25 21 52ZM11 22H26M6 32H18M38 40H55M46 31H60"/>',
 range:'<circle cx="32" cy="32" r="18"/><circle cx="32" cy="32" r="9"/><path d="M32 5V24M32 40V59M5 32H24M40 32H59"/>',
 rate:'<path d="M32 9 22 30H34L25 54 47 26H35L43 9ZM10 22H18M7 33H16M11 44H20"/>',
 bay:'<path d="M10 12H54V52H10ZM32 12V52M10 32H54"/><path d="M17 19H25V25H17ZM39 39H47V45H39Z"/>',
 kinetic:'<path d="M20 49 13 42 38 17Q49 6 55 9 58 15 47 26ZM20 35 29 44M7 53 17 43M7 42 12 37M23 56 28 51"/>',
 laser:'<path d="M8 49 19 56 30 43 19 32ZM25 38 52 11M31 42 57 16M44 9 58 23M49 5V28M38 16H61"/>',
 rocket:'<path d="M22 40Q20 20 49 10 51 36 32 44ZM23 30 11 33 10 46 22 42M34 41 32 54 44 50 46 36M16 47 8 56M22 51 17 58"/><circle cx="38" cy="23" r="5"/>',
 plasma:'<circle cx="32" cy="32" r="11"/><ellipse cx="32" cy="32" rx="25" ry="12" transform="rotate(45 32 32)"/><ellipse cx="32" cy="32" rx="25" ry="12" transform="rotate(-45 32 32)"/>',
 thruster:'<path d="M20 9H44V34H20ZM25 34 20 53 32 45 44 53 39 34M26 16H38V27H26Z"/>',
 vector:'<path d="M32 8V54M8 32H56M24 17 32 8 40 17M24 45 32 54 40 45M17 24 8 32 17 40M47 24 56 32 47 40"/>',
 slipstream:'<path d="M8 17Q25 30 51 17M5 30Q24 43 54 30M8 43Q26 56 51 43"/>',
 cruise:'<circle cx="43" cy="39" r="12"/><path d="M33 36 8 8 24 37M40 27 30 6M30 45 7 31"/>',
 impact:'<path d="M32 9 49 17 45 39 32 53 19 39 15 17ZM23 20V38M32 17V43M41 20V38"/>',
 fortune:'<ellipse cx="32" cy="18" rx="21" ry="8"/><path d="M11 18V43C11 55 53 55 53 43V18M11 30C11 42 53 42 53 30M32 15V21"/>',
 luck:'<rect x="12" y="12" width="40" height="40" rx="5"/><circle cx="22" cy="22" r="3"/><circle cx="42" cy="22" r="3"/><circle cx="32" cy="32" r="3"/><circle cx="22" cy="42" r="3"/><circle cx="42" cy="42" r="3"/>',
 luckyfind:'<path d="m32 7 7 16 18 2-14 12 4 18-15-9-15 9 4-18L7 25l18-2Z"/><circle cx="32" cy="32" r="7"/>',
 collector:'<path d="M14 12V35C14 59 50 59 50 35V12H39V35C39 44 25 44 25 35V12ZM14 24H25M39 24H50M29 16H35M32 13V19"/>',
 harvest:'<path d="m32 9 18 23-18 23L14 32ZM14 32H50M32 9V55M8 17 13 20M51 44 58 48"/>',
 fieldrepair:'<path d="M10 15H54V53H10ZM24 15V9H40V15M27 23H37V30H44V40H37V47H27V40H20V30H27Z"/>'
};
export function skillIcon(key,color,tier){
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 64 64');svg.setAttribute('aria-hidden','true');
 svg.innerHTML=`<g fill="${color}22" stroke="${color}" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">${art[key]||art.strength}</g>`;
 return svg;
}
export const treeCss=`
#arena-skills{position:fixed;inset:2vh 2vw;max-width:none;width:auto;max-height:none;transform:none;padding:18px;display:flex;flex-direction:column;gap:10px;z-index:7;border:2px solid #806844;border-radius:4px;background:linear-gradient(130deg,#1b2530,#090f19 65%);box-shadow:0 0 0 100vmax #02050be8,inset 0 0 0 2px #d8bd7330;color:#ede4cf}
#arena-skills[hidden]{display:none}#arena-skills h2{margin:0;color:#f2d698;font:24px Georgia,serif;letter-spacing:3px}
#arena-skills .tree-heading{display:flex;justify-content:space-between;align-items:center;gap:12px}#arena-skills .tree-heading p{margin:0;color:#f2d698;font-size:12px}
#arena-skills .tree-help{margin:0;font-size:12px;color:#aebbc9}.tree-nav{display:flex;gap:5px;overflow:auto;flex-shrink:0}.tree-nav button{font-size:11px;padding:8px 10px;white-space:nowrap;border-color:#596574!important}
#arena-skills .tree-scroll{overflow:auto;min-height:110px;flex:1;border:1px solid #51616d;background:radial-gradient(circle at 1px 1px,#8197ac18 1px,transparent 2px) 0 0/22px 22px;scrollbar-color:#8d7a53 #111923}
.tree-section{position:relative;width:1040px;box-sizing:border-box;border-bottom:1px solid #405363;padding-top:1px;background:linear-gradient(100deg,color-mix(in srgb,var(--branch) 5%,transparent),transparent 80%)}
#arena-skills h3{margin:12px 18px;font:14px Georgia,serif;letter-spacing:2px;color:var(--branch)}.tree-links{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}.tree-link{fill:none;stroke:#526474;stroke-width:2}.tree-link.unlocked{stroke:var(--branch);opacity:.8}
#arena-skills .skill-node{position:absolute;width:112px;height:100px;padding:0;margin:0;border:0;background:none;color:#bac6d0;display:flex;flex-direction:column;align-items:center;gap:4px;cursor:pointer;font:11px/1.2 monospace}
.skill-face{position:relative;display:block;width:64px;height:64px;box-sizing:border-box;border:3px ridge #67747f;background:radial-gradient(ellipse at 30% 10%,color-mix(in srgb,var(--branch) 28%,#24323d),#0a1527 75%);box-shadow:inset 0 0 12px #000,0 3px 6px #0009}.skill-face svg{width:100%;height:100%;padding:4px;box-sizing:border-box;filter:drop-shadow(0 1px 2px #000)}.skill-tier{position:absolute;bottom:1px;right:3px;font:bold 10px Georgia;color:#fff0be;text-shadow:1px 1px #000}.skill-level{position:absolute;top:-7px;right:-10px;font:9px monospace;padding:2px 3px;background:#17232e;border:1px solid #6b7783;color:#ddd}.skill-name{max-width:112px}
.skill-pips{position:absolute;left:2px;bottom:2px;display:flex;gap:2px}.skill-pips i{width:3px;height:4px;background:var(--branch)}
#arena-skills .skill-node[data-state=learned] .skill-face{border-color:#8ef0b5;background:radial-gradient(ellipse at 30% 20%,#39644d,#0b201e 75%)}
#arena-skills .skill-node[data-state=ready] .skill-face{border-color:#ffe09d;box-shadow:0 0 13px #f2cf6744,inset 0 0 10px #eac95a22}
#arena-skills .skill-node[data-state*=locked] .skill-face{filter:saturate(.45);opacity:.6}
#arena-skills .skill-node:focus-visible{outline:2px solid #fff0b5;outline-offset:3px}#arena-skills .skill-node:hover .skill-face{filter:brightness(1.3)}
#arena-skills .tree-detail{border-top:1px solid #66707a;padding-top:8px;min-height:76px;font-size:12px;color:#bccbd7}.tree-detail strong{font:17px Georgia;color:#f1d69b}.tree-detail p{margin:5px 0}.tree-receipt{color:#a6f0bd;min-height:14px}.tree-footer{display:flex;justify-content:space-between;align-items:center;gap:10px;font-size:10px;color:#9dafbe}
`;
