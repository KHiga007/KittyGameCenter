const $=id=>document.getElementById(id);let games=[],busy=true;
function send(action,id){window.chrome.webview.postMessage({action,id});}

function el(tag,cls,text){const e=document.createElement(tag);if(cls)e.className=cls;if(text!==undefined)e.textContent=text;return e;}
function updatePlatformOptions(){
 const sources=[...new Set(games.map(game=>game.source))];
 if(prefs.platform&&!sources.includes(prefs.platform))sources.push(prefs.platform);
 sources.sort((a,b)=>a.localeCompare(b,'es'));
 const select=$('platform');
 if(select.options.length!==sources.length+1||sources.some((source,i)=>select.options[i+1]?.value!==source)){
  select.replaceChildren(new Option('Todas las plataformas',''),...sources.map(source=>new Option(source,source)));
 }
 select.value=prefs.platform;
}
function render(){
 updatePlatformOptions();
 const list=games.filter(game=>!prefs.platform||game.source===prefs.platform);
 list.sort((a,b)=>(prefs.groupPlatform?a.source.localeCompare(b.source,'es'):0)
  ||a.name.localeCompare(b.name,'es')*(prefs.sort==='desc'?-1:1));
 $('count').textContent=prefs.platform?list.length+'/'+games.length:games.length;
 const grid=$('grid');
 grid.replaceChildren();
 grid.classList.toggle('grouped-games',prefs.groupPlatform&&list.length>0);
 let target=grid,source;
 for(const g of list){
  if(prefs.groupPlatform&&source!==g.source){
   source=g.source;
   const group=el('section','game-group');
   group.append(el('h2','game-group-title',source));
   target=el('div','game-grid');
   group.append(target);
   grid.append(group);
  }
  const c=el('article','game'),cover=el('div','cover');
  const img=el('img','game-image');img.alt=g.name;img.loading='lazy';img.referrerPolicy='no-referrer';
  const fallback='data:image/svg+xml,'+encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="460" height="215" viewBox="0 0 460 215"><defs><linearGradient id="b"><stop stop-color="#41304a"/><stop offset="1" stop-color="#20212f"/></linearGradient></defs><path fill="url(#b)" d="M0 0h460v215H0z"/><g fill="none" stroke="#c696ba" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"><path d="M190 78h80c20 0 36 55 26 65s-28-12-38-18h-56c-10 6-28 28-38 18s6-65 26-65z"/><path d="M192 93v24m-12-12h24"/><circle cx="261" cy="98" r="2"/><circle cx="276" cy="112" r="2"/></g></svg>');
  const steam=/^steam:\/\/rungameid\/(\d+)$/.exec(g.launch||'');
  const previews=g.image?[g.image]:steam?['https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/'+steam[1]+'/header.jpg','https://cdn.akamai.steamstatic.com/steam/apps/'+steam[1]+'/header.jpg']:[];
  let previewIndex=0;img.onerror=()=>{previewIndex++;if(previewIndex<previews.length)img.src=previews[previewIndex];else{img.onerror=null;img.src=fallback;}};
  img.src=previews[0]||fallback;
  cover.append(img);
  const gear=el('button','game-gear');gear.type='button';gear.setAttribute('aria-label','Configurar '+g.name);gear.title='Configurar juego';
  const gearSvg=document.createElementNS('http://www.w3.org/2000/svg','svg');gearSvg.setAttribute('viewBox','0 0 24 24');gearSvg.setAttribute('aria-hidden','true');
  const gearPath=document.createElementNS('http://www.w3.org/2000/svg','path');gearPath.setAttribute('d','M9 3h6l.6 3 2.4 1.4 2.9-.9 3 5.2-2.3 2v2.8l2.3 2-3 5.2-2.9-.9-2.4 1.4-.6 3H9l-.6-3L6 22.8l-2.9.9-3-5.2 2.3-2v-2.8l-2.3-2 3-5.2 2.9.9L8.4 6 9 3Z');
  gearPath.setAttribute('transform','translate(2 0) scale(.83)');const gearCircle=document.createElementNS('http://www.w3.org/2000/svg','circle');gearCircle.setAttribute('cx','12');gearCircle.setAttribute('cy','12');gearCircle.setAttribute('r','3');gearSvg.append(gearPath,gearCircle);gear.append(gearSvg);gear.onclick=()=>openGameSettings(g);cover.append(gear);
  const body=el('div','game-body'),title=el('h3','',g.name);title.title=g.name;body.append(title);
  const actions=el('div','actions'),play=el('button','btn play','▶ Jugar'),folder=el('button','btn folder','');
  const folderIcon=document.createElementNS('http://www.w3.org/2000/svg','svg');folderIcon.setAttribute('viewBox','0 0 24 24');folderIcon.setAttribute('class','folder-icon');folderIcon.setAttribute('aria-hidden','true');
  const folderShape=document.createElementNS('http://www.w3.org/2000/svg','path');folderShape.setAttribute('d','M3 7a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10H3V7Z');folderIcon.append(folderShape);folder.append(folderIcon,document.createTextNode('Carpeta'));
  play.onclick=()=>launchGame(g,img.src);folder.onclick=()=>send('folder',g.id);
  actions.append(play,folder);body.append(actions);c.append(cover,body);target.append(c);
 }
 if(!list.length&&!busy)$('grid').append(el('div','empty',prefs.platform?'No hay juegos para esta plataforma.':'No se encontraron juegos. Añadí una carpeta o un ejecutable.'));
}
const defaults={sort:'asc',density:'normal',platform:'',groupPlatform:false,animation:!window.matchMedia('(prefers-reduced-motion: reduce)').matches,wallpaper:true};
let prefs={...defaults};try{const saved=JSON.parse(localStorage.getItem('kitty-sakura-settings')||'{}');for(const k of Object.keys(defaults))if(typeof saved[k]===typeof defaults[k])prefs[k]=saved[k];if(saved.sort==='source'){prefs.sort='asc';prefs.groupPlatform=true;}}catch{}
function applySettings(){
 if(!['asc','desc'].includes(prefs.sort))prefs.sort='asc';
 if(!['normal','compact','large'].includes(prefs.density))prefs.density='normal';
 $('sort').value=prefs.sort;$('view-'+prefs.density).checked=true;$('group-platform').setAttribute('aria-checked',String(prefs.groupPlatform));$('filters').classList.toggle('has-platform-filter',!!prefs.platform);$('animation').setAttribute('aria-checked',String(prefs.animation));$('wallpaper').setAttribute('aria-checked',String(prefs.wallpaper));
 document.body.classList.toggle('compact',prefs.density==='compact');document.body.classList.toggle('large',prefs.density==='large');document.body.classList.toggle('no-animation',!prefs.animation);document.body.classList.toggle('no-wallpaper',!prefs.wallpaper);
}
applySettings();
function saveAppearancePreferences(){try{localStorage.setItem('kitty-sakura-settings',JSON.stringify(prefs));$('settings-status').textContent='Tus preferencias se guardan automáticamente.';}catch{$('settings-status').textContent='No se pudieron guardar las preferencias.';}applySettings();render();}
for(const id of ['sort','platform'])$(id).onchange=()=>{prefs[id]=$(id).value;saveAppearancePreferences();};
for(const option of document.querySelectorAll('input[name="density"]'))option.onchange=()=>{prefs.density=option.value;saveAppearancePreferences();};
$('group-platform').onclick=()=>{prefs.groupPlatform=!prefs.groupPlatform;saveAppearancePreferences();};
$('filters').onclick=()=>{$('filters-modal').showModal();};
$('close-filters').onclick=()=>$('filters-modal').close();
for(const id of ['animation','wallpaper'])$(id).onclick=()=>{prefs[id]=!prefs[id];saveAppearancePreferences();};
$('settings').onclick=()=>{setRoutesPanel(false,false);$('settings-modal').showModal();};
$('close-settings').onclick=()=>$('settings-modal').close();
for(const id of ['close-launch','dismiss-launch'])$(id).onclick=()=>$('launch-modal').close();
for(const id of ['filters-modal','settings-modal','launch-modal','game-settings-modal','hidden-modal','delete-game-modal'])$(id).addEventListener('click',e=>{if(e.target===$(id)){const r=$(id).getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)$(id).close();}});
let pendingLaunch=null,launchTimer=null;
function launchGame(game,image){
 if(pendingLaunch){$('launch-modal').showModal();return;}
 pendingLaunch=game.id;clearTimeout(launchTimer);
 $('launch-modal').classList.remove('settled','failed');$('launch-image').src=image;$('launch-image').alt=game.name;$('launch-name').textContent=game.name;
 $('launch-title').textContent='Abriendo juego';$('launch-caption').textContent='PREPARANDO TU PARTIDA';$('launch-symbol').textContent='❀';
 $('launch-message').textContent=game.candidate?'Elegí el ejecutable del juego en la ventana de selección.':'Enviando la solicitud de inicio…';
 $('launch-modal').showModal();
 requestAnimationFrame(()=>setTimeout(()=>send('play',game.id),100));
}
function launchResult(m){
 if(m.id!==pendingLaunch)return;
 pendingLaunch=null;
 const finish=()=>{
 $('launch-modal').classList.add('settled');$('launch-modal').classList.toggle('failed',m.state==='error');
 $('launch-symbol').textContent=m.state==='error'?'!':m.state==='cancelled'?'❀':'✓';
 $('launch-caption').textContent=m.state==='error'?'NO SE PUDO INICIAR':m.state==='cancelled'?'VOLVÉ CUANDO QUIERAS':'QUE DISFRUTES TU PARTIDA';
 $('launch-title').textContent=m.state==='error'?'No se pudo abrir':m.state==='cancelled'?'Inicio cancelado':'Solicitud enviada';
 $('launch-message').textContent=m.message;
 };
 // Leave a short moment for the transition; this does not imply the game is running.
 launchTimer=setTimeout(finish,900);
}

let searchRoots=[],automaticRoots=[],automaticDiscovery=true,routeGames=[];
let routesPanelOpen=false,automaticDetailsOpen=false,routeMutationPending=false;
let libraryPanelView='routes',pickerTarget=null,pendingSubmission=null,submittedGameName='',scanRequested=false,lastScanComplete=false;
let currentScanProgress=null;const scanRouteResults=new Map();
const narrowSettings=window.matchMedia('(max-width: 999px)');
const routeCacheKey='kitty-last-automatic-routes';
try{const saved=JSON.parse(localStorage.getItem(routeCacheKey)||'[]');if(Array.isArray(saved))automaticRoots=saved.filter(r=>r&&typeof r.path==='string'&&typeof r.available==='boolean');}catch{}
function normalizeRoute(path){return String(path||'').replaceAll('/','\\').replace(/\\+$/,'').toLocaleLowerCase();}
function gamesInRoute(path){const parent=normalizeRoute(path);return routeGames.filter(g=>{const folder=normalizeRoute(g.folder);return folder===parent||folder.startsWith(parent+'\\');});}
function gameQuantity(count){return count+(count===1?' juego':' juegos');}
function routeGameCount(root){const current=gamesInRoute(root.path).length;return !automaticDiscovery&&typeof root.lastCount==='number'?root.lastCount:current;}
function launcherName(root){
 const p=normalizeRoute(root.path);
 const launchers=[[/steam/,'Steam'],[/epic/,'Epic Games'],[/gog/,'GOG'],[/ubisoft|uplay/,'Ubisoft Connect'],[/ea games|origin/,'EA / Origin'],[/battle\.net|blizzard/,'Battle.net'],[/riot/,'Riot Games'],[/rockstar/,'Rockstar'],[/xbox/,'Xbox']];
 for(const [pattern,name] of launchers)if(pattern.test(p))return name;
 const sources=[...new Set(gamesInRoute(root.path).map(g=>g.source).filter(s=>s&&!['Carpeta','Registro','Manual','Xbox / carpeta'].includes(s)))];
 return sources.length===1?sources[0]:root.launcher||'Biblioteca de juegos';
}
function strokeIcon(path,css=''){const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');if(css)svg.setAttribute('class',css);const shape=document.createElementNS('http://www.w3.org/2000/svg','path');shape.setAttribute('d',path);svg.append(shape);return svg;}
function syncRoutesAccessibility(){const subpage=routesPanelOpen&&narrowSettings.matches;$('settings-page').inert=subpage;$('settings-page').setAttribute('aria-hidden',String(subpage));$('routes-panel').inert=!routesPanelOpen;$('routes-panel').setAttribute('aria-hidden',String(!routesPanelOpen));}
function setRoutesPanel(open,restoreFocus=true){
 routesPanelOpen=open;$('settings-modal').classList.toggle('routes-open',open);
 for(const [id,view] of [['configure-routes','routes'],['add','game']])$(id).setAttribute('aria-expanded',String(open&&libraryPanelView===view));
 $('routes-view').hidden=libraryPanelView!=='routes';$('game-view').hidden=libraryPanelView!=='game';$('routes-status').hidden=libraryPanelView!=='routes';
 $('library-panel-kicker').textContent=libraryPanelView==='routes'?'DÓNDE CRECE TU BIBLIOTECA':'HACÉ CRECER TU BIBLIOTECA';
 $('routes-title').textContent=libraryPanelView==='routes'?'Rutas de búsqueda':'Añadir juego';
 $('library-panel-footnote').textContent=libraryPanelView==='routes'?'Quitar una ruta no borra archivos.':'El juego aparece en tu biblioteca aunque no esté en ninguna ruta.';
 syncRoutesAccessibility();
 if(open){$('close-routes').focus({preventScroll:true});if(libraryPanelView==='routes')send('get-routes');}
 else if(restoreFocus&&$('settings-modal').open)$(libraryPanelView==='routes'?'configure-routes':'add').focus({preventScroll:true});
}
function openLibraryView(view){const same=routesPanelOpen&&libraryPanelView===view;libraryPanelView=view;setRoutesPanel(!same);}
$('configure-routes').onclick=()=>openLibraryView('routes');$('add').onclick=()=>openLibraryView('game');
$('close-routes').onclick=()=>setRoutesPanel(false);
$('settings-modal').addEventListener('cancel',e=>{if(routesPanelOpen){e.preventDefault();setRoutesPanel(false);}});
$('settings-modal').addEventListener('close',()=>setRoutesPanel(false,false));
narrowSettings.addEventListener('change',()=>{syncRoutesAccessibility();if(routesPanelOpen)$('close-routes').focus({preventScroll:true});});
$('toggle-automatic-details').onclick=()=>{automaticDetailsOpen=!automaticDetailsOpen;$('automatic-folders-list').hidden=!automaticDetailsOpen;$('toggle-automatic-details').setAttribute('aria-expanded',String(automaticDetailsOpen));};
$('automatic-routes').onclick=()=>{if(busy||routeMutationPending||pendingSubmission||pickerTarget)return;routeMutationPending=true;window.chrome.webview.postMessage({action:'set-discovery',enabled:!automaticDiscovery});$('routes-status').textContent='Guardando detección…';updateRouteControls();};
function updateRouteControls(){
 const locked=busy||scanRequested||routeMutationPending||!!pendingSubmission||!!pickerTarget;
 $('automatic-routes').disabled=locked;$('add-route').disabled=locked;$('browse-root-path').disabled=locked;$('browse-path').disabled=locked;
 $('save-root-form').disabled=locked||!$('new-root-path').value.trim();
 $('save-add').disabled=locked||!$('add-path').value.trim()||!$('game-name').value.trim();
 $('cancel-root-form').disabled=pendingSubmission==='root';$('cancel-add').disabled=pendingSubmission==='game';
 for(const button of $('routes-list').querySelectorAll('button'))button.disabled=locked;
 renderScanCard();
}
function activeRouteCount(){return new Set([...(automaticDiscovery?automaticRoots.filter(r=>r.available):[]),...searchRoots.filter(r=>r.available)].map(r=>normalizeRoute(r.path))).size;}
function renderScanCard(){
 const scanning=busy||scanRequested,count=activeRouteCount(),p=currentScanProgress;$('scan').disabled=scanning;$('scan-button-label').textContent=scanning?'Buscando…':'Buscar ahora';
 let summary=scanning?'Detectando rutas y launchers…':lastScanComplete?'Listo · '+gameQuantity(routeGames.length)+' en tu biblioteca'+(p&&p.total?' · '+p.total+' rutas revisadas':''):'Revisa tus '+count+' rutas y suma lo nuevo.';
 if(scanning&&p){if(p.phase==='routes')summary='Revisando '+Math.min(p.total,p.completed+(p.state==='scanning'?1:0))+' de '+p.total+' rutas · '+gameQuantity(p.found)+(p.found===1?' detectado':' detectados');else if(p.phase==='previews'||p.phase==='done')summary='Análisis de rutas completo · preparando imágenes…';}
 $('scan-summary').textContent=summary;$('scan-progress').hidden=!scanning;$('scan-progress').setAttribute('aria-busy',String(scanning));
 const determinate=scanning&&p&&p.total>0;const bar=$('scan-progress');bar.classList.toggle('scan-determinate',!!determinate);
 if(determinate){const percentage=Math.round(100*Math.min(p.completed,p.total)/p.total);bar.setAttribute('aria-valuenow',String(percentage));bar.setAttribute('aria-valuemin','0');bar.setAttribute('aria-valuemax','100');bar.firstElementChild.style.width=percentage+'%';}
 else{bar.removeAttribute('aria-valuenow');bar.firstElementChild.style.width='';}
 $('scan-current-path').hidden=!scanning||!p;$('scan-current-path').textContent=p?p.path:'';$('scan-current-path').title=p?p.path:'';
 $('scan-analysis').hidden=scanRouteResults.size===0;$('scan-analysis-count').textContent=scanRouteResults.size?'('+scanRouteResults.size+')':'';
}
function receiveScanProgress(p){
 currentScanProgress=p;
 if(p.phase==='routes'&&p.state!=='scanning'){
  scanRouteResults.set(p.path,p);const list=$('scan-analysis-list');list.replaceChildren();
  for(const row of scanRouteResults.values()){const item=el('li','scan-route-result '+row.state),path=el('span','scan-result-path',row.path);path.title=row.path;const status=row.state==='unavailable'?'No disponible':row.state==='partial'?'Revisada con avisos':'Revisada';item.append(path,el('small','',status+' · '+gameQuantity(row.routeGames)));list.append(item);}
 }
 renderScanCard();
}
$('scan').onclick=()=>{if(busy||scanRequested)return;scanRequested=true;lastScanComplete=false;currentScanProgress=null;scanRouteResults.clear();$('scan-analysis-list').replaceChildren();$('scan-analysis').open=true;renderScanCard();updateRouteControls();send('scan');};
function renderRoutes(){
 const automaticActive=automaticDiscovery?automaticRoots.filter(r=>r.available):[];
 const activePaths=new Set([...automaticActive,...searchRoots.filter(r=>r.available)].map(r=>normalizeRoute(r.path)));
 $('routes-navigation-summary').textContent=activePaths.size+' rutas activas · '+(automaticDiscovery?'detección automática':'solo manuales');
 const badges=$('route-launcher-badges');badges.replaceChildren();
 const names=[...new Set(automaticRoots.filter(r=>r.available).map(launcherName).filter(s=>s!=='Biblioteca de juegos'))];
 for(const name of names.slice(0,5)){const badge=el('span','launcher-initial',name[0].toUpperCase());badge.title=name;badge.setAttribute('aria-label',name);badges.append(badge);}
 if(names.length>5){const more=el('span','launcher-initial','+'+(names.length-5));more.title=names.slice(5).join(', ');badges.append(more);}
 $('automatic-routes').setAttribute('aria-checked',String(automaticDiscovery));
 const totalAutoGames=new Set(automaticRoots.flatMap(r=>gamesInRoute(r.path)).map(g=>g.id)).size;
 $('automatic-summary').textContent=automaticDiscovery?automaticRoots.filter(r=>r.available).length+' de '+automaticRoots.length+' encontradas · '+gameQuantity(totalAutoGames):'En pausa';
 const list=$('automatic-folders-list');list.replaceChildren();list.hidden=!automaticDetailsOpen;list.classList.toggle('detection-paused',!automaticDiscovery);
 for(const root of automaticRoots){const row=el('div','automatic-path-row'),dot=el('span','route-state-dot '+(!automaticDiscovery?'paused':root.available?'found':'missing'));dot.title=!automaticDiscovery?'En pausa':root.available?'Encontrada':'No instalada';const copy=el('div','route-line-copy');copy.append(el('strong','',launcherName(root)));const path=el('span','route-mono',root.path);path.title=root.path;copy.append(path);const count=el('span','route-games-count',gameQuantity(routeGameCount(root)));row.append(dot,copy,count);list.append(row);}
 if(!automaticRoots.length)list.append(el('p','route-list-empty',busy?'Buscando rutas del escaneo…':automaticDiscovery?'El escaneo todavía no encontró rutas automáticas.':'No hay rutas automáticas previas. Activá la detección para encontrarlas.'));
 const manual=$('routes-list');manual.replaceChildren();$('manual-roots-count').textContent=searchRoots.length?searchRoots.length+(searchRoots.length===1?' carpeta':' carpetas'):'Todavía no añadiste carpetas.';
 for(const root of searchRoots){const row=el('div','manual-path-row'),copy=el('div','route-line-copy'),path=el('span','route-mono',root.path);path.title=root.path;copy.append(path,el('span','manual-path-state',gameQuantity(gamesInRoute(root.path).length)+(root.available?'':' · No disponible')));const remove=el('button','remove-route-button');remove.type='button';remove.setAttribute('aria-label','Quitar ruta');remove.title='Quitar '+root.path;remove.append(strokeIcon('m7 7 10 10M17 7 7 17'));remove.onclick=()=>{if(busy||routeMutationPending)return;routeMutationPending=true;$('routes-status').textContent='Quitando ruta…';updateRouteControls();window.chrome.webview.postMessage({action:'remove-root',path:root.path});};row.append(copy,remove);manual.append(row);}

 updateRouteControls();
}
function rememberAutomaticRoutes(){try{localStorage.setItem(routeCacheKey,JSON.stringify(automaticRoots));}catch{}}
function routeMessage(m){
 if(m.type==='scan-progress'){receiveScanProgress(m.progress);return;}
 if(m.type==='refresh-error'||m.type==='error'){busy=false;scanRequested=false;lastScanComplete=false;renderRoutes();$('settings-status').textContent=m.message;}
 if(m.type==='routes'){
  searchRoots=m.roots||[];automaticDiscovery=!!m.automatic;
  // The current backend returns [] while paused. Preserve the last real detection.
  if(automaticDiscovery){automaticRoots=(m.automaticRoots||[]).map(r=>({...r,launcher:launcherName(r),lastCount:gamesInRoute(r.path).length}));rememberAutomaticRoutes();}
  routeMutationPending=false;renderRoutes();
 }
 if(m.type==='routes-error'){routeMutationPending=false;$('routes-status').textContent=m.message;renderRoutes();}
 if(m.type==='busy'){busy=true;scanRequested=false;lastScanComplete=false;currentScanProgress=null;scanRouteResults.clear();$('scan-analysis-list').replaceChildren();renderRoutes();}
 if(m.type==='library'){
  routeGames=m.games||[];busy=!!m.isScanning;if(!busy){scanRequested=false;lastScanComplete=true;}
  if(automaticDiscovery){automaticRoots=automaticRoots.map(r=>({...r,launcher:launcherName(r),lastCount:gamesInRoute(r.path).length}));rememberAutomaticRoutes();}
  renderRoutes();
 }
}
function isAbsoluteEntryPath(path){const slash=String.fromCharCode(92),value=path.replaceAll('/',slash);return (/^[a-z]:/i.test(value)&&value[2]===slash)||(value.startsWith(slash+slash)&&value.slice(2).includes(slash));}
function clearPanelErrors(kind){
 if(kind==='root'){$('root-form-error').textContent='';$('new-root-path').removeAttribute('aria-invalid');}
 else{for(const id of ['game-path-error','game-name-error','add-error'])$(id).textContent='';$('add-path').removeAttribute('aria-invalid');$('game-name').removeAttribute('aria-invalid');}
}
function openRootForm(path=''){
 $('root-inline-form').hidden=false;$('add-route').hidden=true;$('add-route').setAttribute('aria-expanded','true');$('route-drop-help').hidden=true;clearPanelErrors('root');
 if(path)$('new-root-path').value=path;updateRouteControls();$('new-root-path').focus({preventScroll:true});$('root-inline-form').scrollIntoView({block:'nearest',behavior:'auto'});
}
function closeRootForm(){
 if(pendingSubmission==='root')return;$('root-inline-form').reset();$('root-inline-form').hidden=true;$('add-route').hidden=false;$('add-route').setAttribute('aria-expanded','false');$('route-drop-help').hidden=false;clearPanelErrors('root');updateRouteControls();if(routesPanelOpen)$('add-route').focus({preventScroll:true});
}
$('add-route').onclick=()=>openRootForm();$('cancel-root-form').onclick=closeRootForm;
$('new-root-path').oninput=()=>{clearPanelErrors('root');updateRouteControls();};
function guessExecutableName(path){return path.replaceAll('/','\\').split('\\').pop().replace(/\.exe$/i,'');}
function fillExecutableName(name){if(!$('game-name').value.trim())$('game-name').value=name||guessExecutableName($('add-path').value);}
for(const id of ['add-path','game-name'])$(id).oninput=()=>{clearPanelErrors('game');updateRouteControls();};
$('add-path').onchange=()=>{fillExecutableName();updateRouteControls();};
function pickNativePath(kind){
 if(busy||scanRequested||pickerTarget||pendingSubmission||routeMutationPending)return;pickerTarget=kind;clearPanelErrors(kind);updateRouteControls();window.chrome.webview.postMessage({action:kind==='root'?'browse-root':'browse-game'});
}
$('browse-root-path').onclick=()=>pickNativePath('root');$('browse-path').onclick=()=>pickNativePath('game');
$('root-inline-form').onsubmit=e=>{
 e.preventDefault();if(pendingSubmission||busy||scanRequested||routeMutationPending)return;clearPanelErrors('root');
 const path=$('new-root-path').value.trim();if(!path)return;
 if(!isAbsoluteEntryPath(path)){$('root-form-error').textContent='Indicá una ruta completa, por ejemplo D:\\Juegos.';$('new-root-path').setAttribute('aria-invalid','true');return;}
 if(searchRoots.some(r=>normalizeRoute(r.path)===normalizeRoute(path))){$('root-form-error').textContent='Esta carpeta ya está añadida.';$('new-root-path').setAttribute('aria-invalid','true');return;}
 pendingSubmission='root';$('root-form-error').textContent='Añadiendo carpeta…';updateRouteControls();window.chrome.webview.postMessage({action:'save-entry',kind:'root',originalPath:null,name:'',path});
};
$('add-form').onsubmit=e=>{
 e.preventDefault();if(pendingSubmission||busy||scanRequested||routeMutationPending)return;clearPanelErrors('game');$('game-added-notice').hidden=true;
 const path=$('add-path').value.trim(),name=$('game-name').value.trim();
 if(!isAbsoluteEntryPath(path)||!path.toLowerCase().endsWith('.exe')){$('game-path-error').textContent='Elegí la ruta completa de un archivo .exe.';$('add-path').setAttribute('aria-invalid','true');return;}
 if(routeGames.some(game=>normalizeRoute(game.launch)===normalizeRoute(path))){$('game-path-error').textContent='Este ejecutable ya está en tu biblioteca.';$('add-path').setAttribute('aria-invalid','true');return;}
 if(!name||name.length>160){$('game-name-error').textContent='Ingresá un nombre de hasta 160 caracteres.';$('game-name').setAttribute('aria-invalid','true');return;}
 pendingSubmission='game';submittedGameName=name;$('add-error').textContent='Añadiendo juego…';updateRouteControls();window.chrome.webview.postMessage({action:'save-entry',kind:'game',originalPath:null,name,path});
};
$('cancel-add').onclick=()=>{if(pendingSubmission==='game')return;setRoutesPanel(false);};
function libraryPanelMessage(m){
 if(m.type==='picked-path'&&pickerTarget&&((pickerTarget==='root'&&m.kind==='root')||(pickerTarget==='game'&&m.kind==='game'))){
  const target=pickerTarget;pickerTarget=null;clearPanelErrors(target);
  if(m.error)$(target==='root'?'root-form-error':'game-path-error').textContent=m.error;
  else if(m.path){if(target==='root')$('new-root-path').value=m.path;else{$('add-path').value=m.path;fillExecutableName(m.name);}}
  updateRouteControls();if(routesPanelOpen)$(target==='root'?'new-root-path':'add-path').focus({preventScroll:true});
 }
 if(m.type==='entry-saved'&&pendingSubmission){
  const target=pendingSubmission;pendingSubmission=null;clearPanelErrors(target);
  if(target==='root'){closeRootForm();$('routes-status').textContent='Carpeta añadida a tus rutas.';}
  else{$('add-form').reset();$('game-added-text').textContent='«'+submittedGameName+'» se añadió a tu biblioteca.';$('game-added-notice').hidden=false;if(routesPanelOpen&&libraryPanelView==='game')$('add-path').focus({preventScroll:true});}
  updateRouteControls();
 }
 if(m.type==='entry-error'&&pendingSubmission){
  const target=pendingSubmission;pendingSubmission=null;clearPanelErrors(target);
  if(target==='root'){$('root-form-error').textContent=m.message;$('new-root-path').setAttribute('aria-invalid','true');}
  else if(/nombre|caracteres/i.test(m.message)){$('game-name-error').textContent=m.message;$('game-name').setAttribute('aria-invalid','true');}
  else{$('game-path-error').textContent=m.message;$('add-path').setAttribute('aria-invalid','true');}
  updateRouteControls();
 }
}

$('routes-panel').addEventListener('dragover',e=>{if(!busy&&!pendingSubmission){e.preventDefault();e.dataTransfer.dropEffect='copy';$('add-route').classList.add('route-drop-active');}});
$('routes-panel').addEventListener('dragleave',e=>{if(!$('routes-panel').contains(e.relatedTarget))$('add-route').classList.remove('route-drop-active');});
$('routes-panel').addEventListener('drop',e=>{
 e.preventDefault();$('add-route').classList.remove('route-drop-active');if(busy||pickerTarget||pendingSubmission||routeMutationPending)return;
 let path=(e.dataTransfer.getData('text/plain')||e.dataTransfer.getData('text/uri-list')||'').trim().replace(/^"|"$/g,'');
 if(/^file:\/\//i.test(path)){try{const url=new URL(path);path=decodeURIComponent(url.pathname).replace(/^\/(?=[a-z]:)/i,'').replaceAll('/','\\');if(url.hostname)path='\\\\'+url.hostname+path;}catch{path='';}}
 if(/^(?:[a-z]:[\\/]|\\\\[^\\]+\\)/i.test(path)&&!/[\r\n]/.test(path))openRootForm(path);
 else{openRootForm();$('root-form-error').textContent='Elegí la carpeta para obtener su ruta completa.';pickNativePath('root');}
});
renderRoutes();




let selectedGame=null,gameSaving=false,hiddenEntries=[];
function openGameSettings(game){selectedGame=game;gameSaving=false;$('game-display-name').value=game.name;$('game-settings-source').textContent=game.source;$('game-settings-path').value=game.folder;$('game-settings-launch').value=game.launch||'';$('game-settings-launch').placeholder='Elegir ejecutable al jugar';for(const field of ['path','launch']){$('game-settings-'+field).readOnly=true;$('edit-game-'+field).setAttribute('aria-pressed','false');}$('game-settings-error').textContent='';setGameSaving(false);$('game-settings-modal').showModal();}
for(const field of ['path','launch'])$('edit-game-'+field).onclick=()=>{if(gameSaving)return;const input=$('game-settings-'+field);input.readOnly=false;$('edit-game-'+field).setAttribute('aria-pressed','true');input.focus();input.setSelectionRange(input.value.length,input.value.length);};
function setGameSaving(value){gameSaving=value;$('save-game-settings').disabled=value;$('hide-game').disabled=value;}
$('close-game-settings').onclick=()=>{if(!gameSaving)$('game-settings-modal').close();};
$('game-settings-modal').addEventListener('cancel',e=>{if(gameSaving)e.preventDefault();});
$('game-settings-form').onsubmit=e=>{e.preventDefault();if(!selectedGame||gameSaving)return;setGameSaving(true);$('game-settings-error').textContent='Guardando…';window.chrome.webview.postMessage({action:'save-game',id:selectedGame.id,name:$('game-display-name').value.trim(),folder:$('game-settings-path').value.trim(),launch:$('game-settings-launch').value.trim()});};
let deletePending=false;
$('hide-game').onclick=()=>{if(!selectedGame||gameSaving)return;deletePending=false;$('delete-game-name').textContent=selectedGame.name;$('delete-game-confirm').value='';$('delete-game-error').textContent='';$('confirm-delete-game').disabled=true;$('delete-game-modal').showModal();$('delete-game-confirm').focus();};
function validateDelete(){const matches=!!selectedGame&&$('delete-game-confirm').value===selectedGame.name;$('confirm-delete-game').disabled=deletePending||!matches;$('delete-game-error').textContent=$('delete-game-confirm').value&&!matches?'El nombre no coincide. Respetá las mayúsculas y los espacios.':'';}
$('delete-game-confirm').oninput=validateDelete;
for(const id of ['close-delete-game','cancel-delete-game'])$(id).onclick=()=>{if(!deletePending)$('delete-game-modal').close();};
$('delete-game-modal').addEventListener('cancel',e=>{if(deletePending)e.preventDefault();});
$('delete-game-form').onsubmit=e=>{e.preventDefault();if(deletePending||!selectedGame||$('delete-game-confirm').value!==selectedGame.name)return;deletePending=true;setGameSaving(true);$('confirm-delete-game').disabled=true;$('delete-game-error').textContent='Eliminando de la biblioteca…';window.chrome.webview.postMessage({action:'hide-game',id:selectedGame.id,confirmation:$('delete-game-confirm').value});};
function openHiddenGames(){if(document.querySelector('dialog[open]'))return;$('hidden-status').textContent='';renderHidden();$('hidden-modal').showModal();}
window.addEventListener('keydown',e=>{if(e.ctrlKey&&e.shiftKey&&e.code==='KeyH'){e.preventDefault();openHiddenGames();}});
$('close-hidden').onclick=()=>$('hidden-modal').close();
function renderHidden(){
 $('hidden-list').replaceChildren();
 if(!hiddenEntries.length)$('hidden-list').append(el('div','routes-empty','No tenés juegos ocultos.'));
 for(const game of hiddenEntries){const row=el('div','route-item'),name=el('div','route-info',game.name),restore=el('button','btn btn-outline-light','Restaurar');restore.setAttribute('aria-label','Restaurar '+game.name);restore.onclick=()=>{restore.disabled=true;$('hidden-status').textContent='Restaurando juego…';window.chrome.webview.postMessage({action:'restore-game',key:game.key});};row.append(name,restore);$('hidden-list').append(row);}
}
function gameSettingsMessage(m){
 if(m.type==='library'){hiddenEntries=m.hiddenGames||[];renderHidden();}
 if(m.type==='game-saved'){deletePending=false;$('delete-game-modal').close();setGameSaving(false);$('game-settings-modal').close();$('hidden-status').textContent=m.action==='restore-game'?'Juego restaurado.':'';}
 if(m.type==='game-error'){deletePending=false;if($('delete-game-modal').open){$('delete-game-error').textContent=m.message;$('confirm-delete-game').disabled=!selectedGame||$('delete-game-confirm').value!==selectedGame.name;}setGameSaving(false);$('game-settings-error').textContent=m.message;$('hidden-status').textContent=m.message;renderHidden();}
}
renderHidden();


let firstLibraryLoading=false;
function updateLoading(scanning){
 $('loading-card').hidden=true;
 if(firstLibraryLoading){$('startup-description').textContent=games.length?games.length+' juegos encontrados. Preparando sus imágenes…':'Buscando juegos instalados en tu PC…';if(!scanning){firstLibraryLoading=false;$('startup-modal').close();}}
}

window.chrome.webview.addEventListener('message',({data:m})=>{
 if(m.type==='startup'){firstLibraryLoading=!m.hasCache;$('loading-card').hidden=true;if(firstLibraryLoading&&!$('startup-modal').open)$('startup-modal').showModal();else if(!firstLibraryLoading)$('startup-modal').close();}
 gameSettingsMessage(m);
 libraryPanelMessage(m);routeMessage(m);
 if(m.type==='busy'){busy=true;updateLoading(true);$('scan').disabled=true;$('status').textContent='Buscando juegos entre tus mundos…';}
 if(m.type==='library'){games=m.games;busy=!!m.isScanning;$('scan').disabled=busy;$('status').textContent=`${games.length} juegos en tu jardín`+(busy?' · Cargando imágenes…':'');if(m.notes.length>1)$('status').textContent+=' · Algunas ubicaciones no pudieron leerse';render();updateLoading(busy);}
 if(m.type==='refresh-error'||m.type==='cache-warning'){firstLibraryLoading=false;$('startup-modal').close();busy=false;$('scan').disabled=false;renderRoutes();$('loading-card').hidden=false;$('loading-card').classList.add('loading-error');$('loading-title').textContent=m.type==='cache-warning'?'No se pudo guardar la biblioteca':'No se pudo actualizar la biblioteca';$('loading-description').textContent=m.message;}
 if(m.type==='launch')launchResult(m);
 if(m.type==='error'||m.type==='info'){$('status').textContent=m.message;$('settings-status').textContent=m.message;if(m.type==='error'){$('scan').disabled=false;busy=false;}}
});
send('ready');
