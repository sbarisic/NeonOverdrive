export const equipmentStyles = `
#weapon-bench { gap:7px; }
#weapon-bench .forge-body { padding-top:0; padding-bottom:0; }
#weapon-bench .forge-body { overflow: hidden; flex: 1; min-height: 0; }
#weapon-bench #workshop-loadout { height: 100%; display: flex; flex-direction: column; gap: 4px; }
#weapon-bench [hidden] { display: none !important; }
#weapon-bench .equipment-chain { display: grid; grid-template-columns: 108px repeat(8,minmax(0,1fr)); gap: 5px; max-width: 1240px; }
#weapon-bench .equipment-chain .weapon-slot { min-width: 0; min-height: 64px; height: 64px; padding: 5px; }
#weapon-bench .equipment-chain .slot-icon { font-size: 23px; line-height:24px; }
#weapon-bench .equipment-chain .slot-name { font-size: 10px; white-space:nowrap; text-overflow:ellipsis; overflow:hidden; max-width:100%; line-height:12px; }
#weapon-bench .weapon-slot[data-dormant=true] { border-style: dashed; background: #101724; }
#weapon-bench .equipment-middle { display: grid; grid-template-columns: 260px minmax(0,1fr); gap: 10px; height: 216px; flex-shrink: 0; grid-template-rows:minmax(0,1fr); }
#weapon-bench .empty-preview { display:grid; place-items:center; border:1px solid #394652; color:#92aebe; }
#weapon-bench .equipment-middle > * { min-height:0; box-sizing:border-box; }
#weapon-bench .chain-stages { gap:3px; }
#weapon-bench .preview-values { min-height:0; overflow-y:auto; scrollbar-gutter:stable; }
#weapon-bench .chain-stage > span:last-child { min-width:0; overflow-wrap:anywhere; }
#weapon-bench .ship-panel { position: relative; border: 1px solid #617784; background: #09131e; display:grid; grid-template-columns: 1fr 1fr; gap: 6px 42px; padding: 8px; grid-template-rows:repeat(3,minmax(0,1fr)); min-height:0; }
#weapon-bench .ship-silhouette { position:absolute; left:50%; top:50%; transform:translate(-50%,-50%); width:62px; height:132px; pointer-events:none; }
#weapon-bench .ship-silhouette svg { width:100%; height:100%; }
#weapon-bench .ship-socket { z-index:1; padding:5px; min-width:0; font-size:11px; text-align:center; border:1px solid #697981; background:linear-gradient(#1b2b37,#101924); color:#e8d8af; }
#weapon-bench .ship-socket small { display:block; margin-top:4px; font-size:9px; color:#98abbc; }
#weapon-bench .chain-panel { height:100%; margin:0; grid-template-columns:minmax(0,1.2fr) minmax(230px,1fr); padding:7px; }
#weapon-bench .chain-preview { height:164px; }
#weapon-bench .chain-stage { padding:2px 3px; font-size:10px; min-height:26px; height:auto; line-height:1.1; align-items:flex-start; }
#weapon-bench .chain-stage strong, #weapon-bench .chain-stage small { font-size:10px; line-height:11px; }
#weapon-bench .chain-summary { font-size:10px; line-height:12px; gap:2px; padding-bottom:5px; }
#weapon-bench .chain-caption { font-size:10px; }
#weapon-bench .inventory-toolbar { flex-shrink:0; display:flex; gap:5px; align-items:center; }
#weapon-bench .inventory-toolbar strong { margin-right:10px; font-size:11px; }
#weapon-bench .inventory-grid { position:relative; overflow:auto; flex:1; min-height:74px; border:1px solid #394652; background:#070f1a; }
#weapon-bench .inventory-window { position:absolute; left:0; right:0; display:grid; gap:5px; padding:5px; }
#weapon-bench .inventory-cell { display:flex; flex-direction:column; justify-content:center; height:68px; padding:3px; font-size:10px; min-width:0; border:1px solid #536774; background:#111d28; color:#c5d4df; }
#weapon-bench .inventory-cell b { font-size:22px; color:#ecd18a; }
#weapon-bench .inventory-cell small { font-size:9px; color:#8dbcc9; }
#weapon-bench button[data-selected=true] { outline:2px solid #ecd18a; outline-offset:-2px; }
#weapon-bench .inventory-toolbar button, #weapon-bench .weapon-selector button { min-height:27px; padding:4px 8px; font-size:11px; }
#weapon-bench .bench-detail { flex-shrink:0; }
#weapon-bench .forge-help { margin:4px 0; }
#weapon-bench #workshop-shop { height:100%; overflow:auto; }
#weapon-bench .weapon-selector { margin:0; }
#weapon-bench .weapon-heading { margin:0; min-height:28px; flex-shrink:0; }
#weapon-bench .weapon-heading button { min-height:28px; height:28px; padding:4px 8px; }
#weapon-bench .equipment-chain, #weapon-bench .weapon-selector { flex-shrink:0; }
@media (max-height:800px) {
 #weapon-bench .equipment-middle { height:172px; }
 #weapon-bench .chain-preview { height:132px; }
 #weapon-bench .chain-note { display:none; }
 #weapon-bench .equipment-chain .weapon-slot { height:58px; min-height:58px; }
 #weapon-bench .bench-detail { min-height:32px; }
}

#weapon-bench [data-gear-category="0"] { --gear-color:#60A5FA; }
#weapon-bench [data-gear-category="1"] { --gear-color:#FB923C; }
#weapon-bench [data-gear-category="2"] { --gear-color:#C084FC; }
#weapon-bench [data-gear-category="3"] { --gear-color:#4ADE80; }
#weapon-bench [data-gear-category="4"] { --gear-color:#FB7185; }
#weapon-bench [data-gear-category="5"] { --gear-color:#FACC15; }
#weapon-bench [data-gear-category] { border-color:var(--gear-color); color:var(--gear-color); }
#weapon-bench [data-gear-category] b { color:var(--gear-color); }
#weapon-bench .equipment-drop-target { outline:2px solid var(--gear-color, #edd28c); outline-offset:2px; }
#weapon-bench [data-gear-category]:focus-visible { outline:2px solid var(--gear-color); }
`;
