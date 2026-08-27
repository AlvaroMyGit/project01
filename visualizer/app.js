// S.T.A.L.K.E.R. A-Life Visualizer — PixiJS v7 + pixi-viewport
// Phase 10: pixel sprites, paperdoll, corpses, roof cutaway, on-demand inspect.

// ─── Constants ──────────────────────────────────────────────────────────────
const WS_URL    = 'ws://localhost:8080/';
const API_WORLD = 'http://localhost:5050/api/world';

const FACTION_COLORS = {
    Loner:       0xa1a1aa,
    Duty:        0xef4444,
    Freedom:     0x22c55e,
    Bandit:      0x78350f,
    Mercenary:   0x3b82f6,
    ClearSky:    0x06b6d4,
    Monolith:    0xf3f4f6,
    Ecologist:   0xfbbf24,
    Zombified:   0x7c3aed,
    Military:    0x84cc16,
    Mutants:     0xd946ef,
};

const SPRITE_MAP = {
    Loner:       'stalker_loner.png',
    Duty:        'stalker_duty.png',
    Freedom:     'stalker_freedom.png',
    Bandit:      'stalker_bandit.png',
    Mercenary:   'stalker_mercenary.png',
    ClearSky:    'stalker_clearsky.png',
    Monolith:    'stalker_monolith.png',
    Ecologist:   'stalker_ecologist.png',
    Zombified:   'stalker_zombified.png',
    Military:    'stalker_military.png',
    Mutants:     'mutant_default.png',
};

// LOD zoom thresholds
const LOD_RADAR     = 0.15;
const LOD_ICON      = 0.50;
const LOD_SPRITE    = 1.20;
const LOD_PAPERDOLL = 2.50;
const ROOF_CUTAWAY  = 1.00;

// ─── App State ───────────────────────────────────────────────────────────────
let app, viewport;
let worldData = null;
let entities  = [];
let corpses   = [];
let missionStats = null;
let anomalyFields = [];
let isStormActive = false;
let emissionPhase = 'Dormant';

let activeLayer       = 0;
let selectedEntityId  = null;
let selectedCorpseId  = null;
let followEntityId    = null;
let _lastFrame        = null;

// Pixi containers
let bgContainer, wildernessContainer, buildingContainer, roadContainer, poiContainer, roofContainer;
let anomalyContainer, radZoneContainer, corpseContainer, missionContainer, squadContainer, entityContainer, labelContainer;
let stormOverlay;

// Texture cache
const textureCache = new Map();
let paperdollWeaponTex = null;
let paperdollArmorTex  = null;
let corpseTex          = null;

// Entity / corpse sprite pools
const entityPool = new Map();
const corpsePool = new Map();

// Macro-base roof cutaway
let cutawayBaseId = null;

// ─── Init ─────────────────────────────────────────────────────────────────────
export async function init(canvasEl, onEntitySelect) {
    _onEntitySelect = onEntitySelect;

    app = new PIXI.Application({
        view: canvasEl,
        resizeTo: canvasEl.parentElement,
        backgroundColor: 0x0b0c10,
        antialias: true,
        resolution: window.devicePixelRatio || 1,
        autoDensity: true,
    });

    viewport = new PIXI.Viewport({
        screenWidth:  app.screen.width,
        screenHeight: app.screen.height,
        worldWidth:   1600,
        worldHeight:  3200,
        events: app.renderer.events,
    });
    app.stage.addChild(viewport);

    viewport
        .drag({ mouseButtons: 'left' })
        .pinch()
        .wheel()
        .decelerate({ friction: 0.94 })
        .clampZoom({ minScale: 0.08, maxScale: 4.0 });

    window.addEventListener('resize', () => {
        app.renderer.resize(canvasEl.parentElement.clientWidth, canvasEl.parentElement.clientHeight);
        viewport.resize(app.screen.width, app.screen.height);
        stormOverlay.width  = app.screen.width;
        stormOverlay.height = app.screen.height;
    });

    bgContainer         = new PIXI.Container();
    wildernessContainer = new PIXI.Container();
    buildingContainer   = new PIXI.Container();
    roadContainer       = new PIXI.Container();
    poiContainer     = new PIXI.Container();
    roofContainer    = new PIXI.Container();
    anomalyContainer = new PIXI.Container();
    radZoneContainer = new PIXI.Container();
    corpseContainer  = new PIXI.Container();
    missionContainer = new PIXI.Container();
    squadContainer   = new PIXI.Container();
    entityContainer  = new PIXI.Container();
    labelContainer   = new PIXI.Container();

    viewport.addChild(bgContainer);
    viewport.addChild(wildernessContainer);
    viewport.addChild(buildingContainer);
    viewport.addChild(roadContainer);
    viewport.addChild(poiContainer);
    viewport.addChild(roofContainer);
    viewport.addChild(radZoneContainer);
    viewport.addChild(anomalyContainer);
    viewport.addChild(corpseContainer);
    viewport.addChild(missionContainer);
    viewport.addChild(squadContainer);
    viewport.addChild(entityContainer);
    viewport.addChild(labelContainer);

    stormOverlay = new PIXI.Graphics();
    stormOverlay.beginFill(0xff6600, 0.0);
    stormOverlay.drawRect(0, 0, app.screen.width, app.screen.height);
    stormOverlay.endFill();
    app.stage.addChild(stormOverlay);

    entityContainer.eventMode = 'static';
    viewport.on('zoomed', () => { updateLOD(); updateRoofCutaway(); });
    viewport.on('moved', updateRoofCutaway);
    viewport.on('drag-start', () => {
        if (followEntityId) stopFollow();
    });

    drawBackgroundGrid();
    await loadSprites();
    await fetchWorld();

    app.ticker.add(tick);
    connectWebSocket();
}

let _onEntitySelect = null;
let _onInspectorData = null;
let _onStormChange   = null;
let _onFollowChange  = null;
let _stormPulse = 0;

export function setInspectorCallback(fn) { _onInspectorData = fn; }
export function setStormCallback(fn)     { _onStormChange = fn; }
export function setFollowCallback(fn)  { _onFollowChange = fn; }

function notifyFollowChange() {
    if (!_onFollowChange) return;
    const ent = followEntityId ? entities.find(e => e.id === followEntityId) : null;
    _onFollowChange({
        active: !!followEntityId,
        entityId: followEntityId,
        name: ent?.name ?? null,
    });
}

export function stopFollow() {
    if (!followEntityId) return;
    followEntityId = null;
    notifyFollowChange();
}

export function isFollowing() {
    return followEntityId != null;
}

export function getFollowEntityId() {
    return followEntityId;
}

export function requestInspect(entityId) {
    if (ws?.readyState === WebSocket.OPEN && entityId) {
        ws.send(JSON.stringify({ type: 'inspect', entityId }));
    }
}

export function focusEntity(entityId, { follow = false } = {}) {
    if (!entityId) return;
    selectedEntityId = entityId;
    selectedCorpseId = null;

    if (follow) followEntityId = entityId;
    else if (followEntityId && followEntityId !== entityId) stopFollow();

    const ent = entities.find(e => e.id === entityId);
    if (ent?.position) {
        viewport.moveCenter(wX(ent.position.x), wY(ent.position.y));
        if (viewport.scale.x < LOD_SPRITE) viewport.setZoom(1.4, true);
    }

    requestInspect(entityId);
    if (ent && _onEntitySelect) _onEntitySelect(ent);
    notifyFollowChange();
    if (_lastFrame) updateEntities(_lastFrame);
}

export function getSelectedEntityId() {
    return selectedEntityId;
}

function tick(delta) {
    updateLOD();

    if (isStormActive) {
        _stormPulse += 0.04 * delta;
        const alpha = 0.12 + Math.sin(_stormPulse) * 0.08;
        stormOverlay.clear();
        stormOverlay.beginFill(0xff4400, alpha);
        stormOverlay.drawRect(0, 0, app.screen.width, app.screen.height);
        stormOverlay.endFill();
    } else {
        _stormPulse = 0;
        stormOverlay.clear();
    }
}

// ─── Sprite Loading ───────────────────────────────────────────────────────────
async function loadTexture(name) {
    if (textureCache.has(name)) return textureCache.get(name);
    try {
        const tex = await PIXI.Assets.load(`assets/${name}`);
        textureCache.set(name, tex);
        return tex;
    } catch (e) {
        console.warn(`[App] Missing sprite: ${name}`);
        return null;
    }
}

async function loadSprites() {
    const names = [...new Set(Object.values(SPRITE_MAP))];
    await Promise.all(names.map(loadTexture));
    paperdollWeaponTex = await loadTexture('paperdoll_weapon.png');
    paperdollArmorTex  = await loadTexture('paperdoll_armor.png');
    corpseTex          = await loadTexture('corpse.png');
}

function spriteForEntity(ent) {
    if (ent.type === 'mutant') return textureCache.get('mutant_default.png');
    const file = SPRITE_MAP[ent.faction] ?? SPRITE_MAP.Loner;
    return textureCache.get(file);
}

// ─── Background Grid ─────────────────────────────────────────────────────────
function drawBackgroundGrid() {
    const g = new PIXI.Graphics();
    g.lineStyle(1, 0x1a2233, 0.5);
    const cols = 20, rows = 40;
    for (let i = 0; i <= cols; i++) {
        const x = (i / cols) * 1600;
        g.moveTo(x, 0); g.lineTo(x, 3200);
    }
    for (let j = 0; j <= rows; j++) {
        const y = (j / rows) * 3200;
        g.moveTo(0, y); g.lineTo(1600, y);
    }
    bgContainer.addChild(g);
}

// ─── World Data ───────────────────────────────────────────────────────────────
async function fetchWorld() {
    try {
        const res = await fetch(API_WORLD);
        worldData = await res.json();
        drawWildernessTint();
        drawBuildings();
        drawRoads();
        drawPOIs();
        drawRadZones();
    } catch (e) {
        console.warn('[App] Could not fetch world data — server may be starting up.');
    }
}

function drawWildernessTint() {
    wildernessContainer.removeChildren();
    if (!worldData?.threatMap || !worldData.threatW || !worldData.threatH) return;

    const w = worldData.width ?? 1600;
    const h = worldData.height ?? 3200;
    const tw = worldData.threatW;
    const th = worldData.threatH;
    const cellW = w / tw;
    const cellH = h / th;
    const g = new PIXI.Graphics();

    for (let j = 0; j < th; j++) {
        for (let i = 0; i < tw; i++) {
            const t = worldData.threatMap[j * tw + i] ?? 0;
            const color = t < 0.22 ? 0x1a3d2e : t < 0.40 ? 0x2d4a22 : t < 0.60 ? 0x4a4020 : t < 0.82 ? 0x4a2818 : 0x3a1010;
            g.beginFill(color, 0.22 + t * 0.18);
            g.drawRect(i * cellW, j * cellH, cellW + 1, cellH + 1);
            g.endFill();
        }
    }
    wildernessContainer.addChild(g);
}

function drawBuildings() {
    buildingContainer.removeChildren();
    if (!worldData?.buildings) return;

    worldData.buildings.forEach(b => {
        const g = new PIXI.Graphics();
        const x = b.centerX - b.width * 0.5;
        const y = b.centerZ - b.depth * 0.5;
        const isMacro = b.poiType === 'MacroBase';
        const t = b.threatLevel ?? 0.3;

        const fill = isMacro ? 0x3d3428 : 0x2a2a2a;
        const stroke = isMacro ? 0xffa500 : 0x666666;
        const alpha = isMacro ? 0.55 : 0.4;

        g.lineStyle(isMacro ? 2 : 1, stroke, 0.85);
        g.beginFill(fill, alpha);
        g.drawRect(x, y, b.width, b.depth);
        g.endFill();

        if (b.hasInterior) {
            g.lineStyle(1, 0x88ccff, 0.7);
            g.beginFill(0x1a2530, 0.35);
            const inset = isMacro ? 6 : 3;
            g.drawRect(x + inset, y + inset, b.width - inset * 2, b.depth - inset * 2);
            g.endFill();

            g.lineStyle(0);
            g.beginFill(0xffdd88, 0.9);
            g.drawRect(b.doorX - 3, b.doorZ - 2, 6, 4);
            g.endFill();
        }

        buildingContainer.addChild(g);

        if (isMacro && viewport.scale.x >= 0.25) {
            const lbl = new PIXI.Text(b.name, {
                fontFamily: 'monospace',
                fontSize: 8,
                fill: 0xcccccc,
            });
            lbl.position.set(b.centerX - lbl.width * 0.5, b.centerZ - 4);
            buildingContainer.addChild(lbl);
        }
    });
}

function drawRoads() {
    roadContainer.removeChildren();
    if (!worldData?.roads) return;

    worldData.roads.forEach(road => {
        const pts = road.waypoints;
        if (!pts || pts.length < 2) return;

        const g = new PIXI.Graphics();
        const t = road.threatLevel ?? 0;
        const color = t < 0.3 ? 0x6ab04c : t < 0.6 ? 0xf9ca24 : t < 0.85 ? 0xe55039 : 0xc0392b;
        const underground = road.type === 'Underground';

        g.lineStyle({
            width: underground ? 2 : 3,
            color: underground ? 0x00dcff : color,
            alpha: underground ? 0.5 : 0.65,
        });

        g.moveTo(wX(pts[0].x), wY(pts[0].y));
        for (let i = 1; i < pts.length; i++) {
            g.lineTo(wX(pts[i].x), wY(pts[i].y));
        }
        roadContainer.addChild(g);
    });
}

function drawPOIs() {
    poiContainer.removeChildren();
    if (!worldData?.pois) return;

    worldData.pois.forEach(poi => {
        if (poi.type === 'MacroBase') return; // drawn in roof layer

        const g = new PIXI.Graphics();
        const x = wX(poi.x), y = wY(poi.y);

        let color, radius;
        switch (poi.type) {
            case 'MicroShelter':    color = 0xffff00; radius = 4;  break;
            case 'UndergroundLab':  color = 0x00ffff; radius = 5;  break;
            case 'MutantDen':       color = 0xd946ef; radius = 3;  break;
            default:                color = 0x888888; radius = 2;
        }

        g.lineStyle(1, 0x000000, 0.6);
        g.beginFill(color, 0.9);
        g.drawCircle(0, 0, radius);
        g.endFill();
        g.position.set(x, y);
        poiContainer.addChild(g);
    });
}

function updateRoofCutaway() {
    roofContainer.removeChildren();
    if (!worldData?.pois || viewport.scale.x < ROOF_CUTAWAY) {
        cutawayBaseId = null;
        return;
    }

    const center = viewport.center;
    const macroBases = worldData.pois.filter(p => p.type === 'MacroBase');
    let nearest = null;
    let nearestDist = Infinity;

    macroBases.forEach(poi => {
        const dx = center.x - wX(poi.x);
        const dy = center.y - wY(poi.y);
        const dist = Math.hypot(dx, dy);
        if (dist < nearestDist) {
            nearestDist = dist;
            nearest = poi;
        }
    });

    const cutawayRadius = 120;
    cutawayBaseId = (nearest && nearestDist < cutawayRadius) ? nearest.id ?? nearest.name : null;

    macroBases.forEach(poi => {
        const x = wX(poi.x), y = wY(poi.y);
        const building = worldData?.buildings?.find(b =>
            b.name === poi.name || b.poiId === poi.id);

        const g = new PIXI.Graphics();
        const isCutaway = cutawayBaseId && (poi.id === cutawayBaseId || poi.name === cutawayBaseId);

        if (building && isCutaway) {
            g.lineStyle(2, 0x88ccff, 0.95);
            g.beginFill(0x1a2530, 0.25);
            g.drawRect(
                building.centerX - building.width * 0.5,
                building.centerZ - building.depth * 0.5,
                building.width,
                building.depth
            );
            g.endFill();
        } else if (isCutaway) {
            g.lineStyle(2, 0xffa500, 0.9);
            g.beginFill(0xffa500, 0.08);
            g.drawCircle(0, 0, 18);
            g.endFill();
        } else {
            g.lineStyle(1, 0x000000, 0.4);
            g.beginFill(0xffa500, 0.0);
            g.drawCircle(0, 0, 5);
            g.endFill();
        }
        g.position.set(x, y);
        roofContainer.addChild(g);

        if (isCutaway || !building) {
            const lbl = new PIXI.Text(poi.name, {
                fontFamily: 'monospace',
                fontSize: 9,
                fill: 0xffffff,
                alpha: isCutaway ? 1.0 : 0.75,
            });
            lbl.position.set(x + 9, y - 4);
            roofContainer.addChild(lbl);
        }
    });
}

// ─── Anomaly Fields ───────────────────────────────────────────────────────────
function drawAnomalyFields(fields) {
    anomalyContainer.removeChildren();
    fields.forEach(f => {
        const g = new PIXI.Graphics();
        const x = wX(f.center.x), y = wY(f.center.z ?? f.center.y);
        const r = (f.radius ?? 40) * (1600 / (worldData?.width ?? 1600));

        const colorMap = { Electro: 0xfbbf24, Fire: 0xef4444, Chemical: 0x22c55e, Psi: 0x8b5cf6, Gravitational: 0x06b6d4 };
        const color = colorMap[f.type] ?? 0xff6600;

        g.lineStyle(1.5, color, 0.7);
        g.beginFill(color, 0.12);
        g.drawCircle(0, 0, r);
        g.endFill();
        g.position.set(x, y);
        anomalyContainer.addChild(g);
    });
}

// ─── Radiation Zones ──────────────────────────────────────────────────────────
function drawRadZones() {
    radZoneContainer.removeChildren();
    if (!worldData?.radZones) return;

    worldData.radZones.forEach(z => {
        const g = new PIXI.Graphics();
        const x = wX(z.x), y = wY(z.y);
        const r = z.radius * (1600 / (worldData.width ?? 1600));

        // Red-green gradient look using multiple rings for a pulsing effect
        g.lineStyle(2, 0xff0000, 0.4 * z.intensity);
        g.beginFill(0xffaa00, 0.08 * z.intensity);
        g.drawCircle(0, 0, r);
        g.endFill();
        
        g.lineStyle(1, 0xff0000, 0.2 * z.intensity);
        g.drawCircle(0, 0, r * 0.9);
        
        g.position.set(x, y);
        radZoneContainer.addChild(g);
    });
}

// ─── Corpse Layer ─────────────────────────────────────────────────────────────
function corpseHasLoot(c) {
    if (c.isLooted || !c.loot) return false;
    return !!(c.loot.primaryWeapon || c.loot.secondaryWeapon || c.loot.armor || c.loot.helmet);
}

function updateCorpses(frameCorpses) {
    corpses = frameCorpses ?? [];
    if (activeLayer !== 0) {
        corpsePool.forEach((obj, id) => {
            corpseContainer.removeChild(obj.gfx);
            corpsePool.delete(id);
        });
        return;
    }

    const alive = new Set(corpses.map(c => c.id));
    corpsePool.forEach((obj, id) => {
        if (!alive.has(id)) {
            corpseContainer.removeChild(obj.gfx);
            corpsePool.delete(id);
        }
    });

    const zoom = viewport.scale.x;
    corpses.forEach(corpse => {
        const x = wX(corpse.position.x);
        const y = wY(corpse.position.y);

        let obj = corpsePool.get(corpse.id);
        if (!obj) {
            const gfx = new PIXI.Container();
            gfx.eventMode = 'static';
            gfx.cursor = 'pointer';
            gfx.on('pointerdown', () => {
                selectedCorpseId = corpse.id;
                selectedEntityId = null;
                stopFollow();
                if (_onEntitySelect) _onEntitySelect({ ...corpse, type: corpse.isMutant ? 'corpse_mutant' : 'corpse' });
                requestInspect(corpse.id);
            });
            corpseContainer.addChild(gfx);
            obj = { gfx };
            corpsePool.set(corpse.id, obj);
        }

        const { gfx } = obj;
        gfx.removeChildren();
        gfx.position.set(x, y);

        const selected = corpse.id === selectedCorpseId;
        const hasLoot = corpseHasLoot(corpse);
        let alpha = 1.0;
        if (corpse.isLooted) alpha = 0.35;
        else if (corpse.isReported) alpha = 0.55;

        const baseColor = corpse.isMutant ? 0x4a044e : 0x661111;

        if (zoom >= LOD_SPRITE && corpseTex) {
            const spr = new PIXI.Sprite(corpseTex);
            spr.anchor.set(0.5, 0.5);
            spr.width = 32; spr.height = 32;
            spr.alpha = alpha;
            spr.tint = corpse.isMutant ? 0xaa66cc : 0xffffff;
            gfx.addChild(spr);
        } else if (zoom >= LOD_ICON) {
            const g = new PIXI.Graphics();
            g.beginFill(baseColor, 0.85 * alpha);
            g.drawRect(-3, -3, 6, 6);
            g.endFill();
            gfx.addChild(g);
        } else {
            const g = new PIXI.Graphics();
            g.beginFill(baseColor, 0.6 * alpha);
            g.drawRect(-1, -1, 2, 2);
            g.endFill();
            gfx.addChild(g);
        }

        if (selected) {
            const ring = new PIXI.Graphics();
            ring.lineStyle(1.5, 0xffffff, 0.9);
            ring.drawCircle(0, 0, zoom >= LOD_SPRITE ? 12 : 6);
            gfx.addChild(ring);
        }

        if (hasLoot && !corpse.isLooted) {
            const dot = new PIXI.Graphics();
            dot.beginFill(0x22c55e, 0.95);
            dot.drawCircle(zoom >= LOD_SPRITE ? 10 : 4, zoom >= LOD_SPRITE ? -10 : -4, 2);
            dot.endFill();
            gfx.addChild(dot);
        }

        if (corpse.despawnSec != null && corpse.despawnSec > 0 && corpse.despawnSec < 300) {
            const urgency = 1 - (corpse.despawnSec / 300);
            const ring = new PIXI.Graphics();
            ring.lineStyle(1, 0xef4444, 0.4 + urgency * 0.5);
            ring.drawCircle(0, 0, zoom >= LOD_SPRITE ? 14 : 7);
            gfx.addChild(ring);
        }
    });
}

function missionTypeLabel(type) {
    return { ScoutPoi: 'Scout', RetrieveStash: 'Retrieve', EscortConvoy: 'Escort' }[type] ?? type ?? 'Job';
}

function updateSquadLinks(frameEntities) {
    squadContainer.removeChildren();
    const zoom = viewport.scale.x;
    if (zoom < LOD_ICON) return;

    const squads = {};
    (frameEntities ?? []).forEach(ent => {
        if (!ent.squadId || ent.type !== 'stalker') return;
        if (!squads[ent.squadId]) squads[ent.squadId] = { leader: null, members: [] };
        if (ent.isSquadLeader) squads[ent.squadId].leader = ent;
        else squads[ent.squadId].members.push(ent);
    });

    Object.values(squads).forEach(squad => {
        if (!squad.leader) return;
        const lx = wX(squad.leader.position.x);
        const ly = wY(squad.leader.position.y);
        const color = FACTION_COLORS[squad.leader.faction] ?? 0xffffff;

        squad.members.forEach(member => {
            const mx = wX(member.position.x);
            const my = wY(member.position.y);
            const line = new PIXI.Graphics();
            line.lineStyle(1, color, 0.4);
            line.moveTo(lx, ly);
            line.lineTo(mx, my);
            
            // Draw a subtle arrowhead indicating connection direction (to member)
            const angle = Math.atan2(my - ly, mx - lx);
            const arrowLen = zoom >= LOD_SPRITE ? 5 : 3;
            line.moveTo(mx, my);
            line.lineTo(mx - arrowLen * Math.cos(angle - Math.PI / 6), my - arrowLen * Math.sin(angle - Math.PI / 6));
            line.moveTo(mx, my);
            line.lineTo(mx - arrowLen * Math.cos(angle + Math.PI / 6), my - arrowLen * Math.sin(angle + Math.PI / 6));

            squadContainer.addChild(line);
        });
    });
}

function updateMissionOverlays(frameEntities) {
    missionContainer.removeChildren();
    const zoom = viewport.scale.x;
    if (zoom < LOD_ICON) return;

    (frameEntities ?? []).forEach(ent => {
        const m = ent.mission;
        if (!m?.targetPosition || ent.type !== 'stalker') return;

        const x1 = wX(ent.position.x);
        const y1 = wY(ent.position.y);
        const returning = !!m.objectiveDone && m.issuerPosition;
        const dest = returning ? m.issuerPosition : m.targetPosition;
        const x2 = wX(dest.x);
        const y2 = wY(dest.y);
        const lineColor = returning ? 0x34d399 : 0xfbbf24;

        const line = new PIXI.Graphics();
        line.lineStyle(1, lineColor, returning ? 0.55 : 0.45);
        line.moveTo(x1, y1);
        line.lineTo(x2, y2);
        missionContainer.addChild(line);

        const marker = new PIXI.Graphics();
        marker.lineStyle(1.5, lineColor, 0.85);
        marker.beginFill(lineColor, returning ? 0.35 : 0.25);
        marker.drawCircle(x2, y2, zoom >= LOD_SPRITE ? 6 : 4);
        marker.endFill();
        missionContainer.addChild(marker);
    });
}

function updateFollowCamera() {
    if (!followEntityId) return;

    const ent = entities.find(e => e.id === followEntityId);
    if (!ent?.position) {
        stopFollow();
        return;
    }

    viewport.moveCenter(wX(ent.position.x), wY(ent.position.y));
}

// ─── Entity Rendering ─────────────────────────────────────────────────────────
function layerMatches(ent) {
    const layer = ent.layerIndex ?? 0;
    return layer === activeLayer;
}

function updateEntities(frame) {
    _lastFrame = frame;
    entities       = frame.entities    ?? [];
    missionStats   = frame.missionStats ?? missionStats;
    if (_onMissionStats && missionStats) _onMissionStats(missionStats);
    isStormActive  = frame.stormActive ?? false;
    emissionPhase  = frame.emissionPhase ?? 'Dormant';
    const fields   = frame.anomalyFields ?? [];

    if (_onStormChange) _onStormChange(isStormActive, emissionPhase);

    drawAnomalyFields(fields);
    updateCorpses(frame.corpses);
    updateMissionOverlays(entities);
    updateSquadLinks(entities);
    updateRoofCutaway();
    updateFollowCamera();

    const alive = new Set(entities.map(e => e.id));
    entityPool.forEach((obj, id) => {
        if (!alive.has(id)) {
            entityContainer.removeChild(obj.root);
            labelContainer.removeChild(obj.label);
            entityPool.delete(id);
        }
    });

    entities.forEach(ent => {
        if (!layerMatches(ent)) {
            const stale = entityPool.get(ent.id);
            if (stale) stale.root.visible = false;
            return;
        }

        const x = wX(ent.position.x);
        const y = wY(ent.position.y);
        const selected = ent.id === selectedEntityId;
        const followed = ent.id === followEntityId;
        const color = FACTION_COLORS[ent.faction] ?? 0xaaaaaa;
        const zoom = viewport.scale.x;
        const isInDesperation = ent.desperation;
        const onMission = !!ent.mission;

        let obj = entityPool.get(ent.id);
        if (!obj) {
            const root = new PIXI.Container();
            root.eventMode = 'static';
            root.cursor = 'pointer';
            root.on('pointerdown', () => {
                selectedEntityId = ent.id;
                selectedCorpseId = null;
                followEntityId = ent.id;
                notifyFollowChange();
                if (_onEntitySelect) _onEntitySelect(ent);
                requestInspect(ent.id);
            });

            const gfx = new PIXI.Graphics();
            const sprite = new PIXI.Sprite();
            sprite.anchor.set(0.5, 0.5);
            sprite.visible = false;

            const paperdoll = new PIXI.Container();
            paperdoll.visible = false;

            root.addChild(gfx);
            root.addChild(sprite);
            root.addChild(paperdoll);

            const label = new PIXI.Text('', { fontFamily: 'monospace', fontSize: 8, fill: 0xffffff, alpha: 0.8 });
            entityContainer.addChild(root);
            labelContainer.addChild(label);
            obj = { root, gfx, sprite, paperdoll, label };
            entityPool.set(ent.id, obj);
        }

        const { root, gfx, sprite, paperdoll, label } = obj;
        root.visible = true;
        root.position.set(x, y);
        gfx.clear();
        sprite.visible = false;
        paperdoll.removeChildren();
        paperdoll.visible = false;

        if (zoom < LOD_RADAR) {
            if (followed) {
                gfx.lineStyle(1, 0x66fcf1, 0.9);
                gfx.drawRect(-2.5, -2.5, 5, 5);
            }
            gfx.beginFill(color, 0.85);
            gfx.drawRect(-1.5, -1.5, 3, 3);
            gfx.endFill();
            label.visible = false;
        } else if (zoom < LOD_ICON) {
            if (selected) {
                gfx.lineStyle(1.5, 0xffffff, 1.0);
                gfx.drawCircle(0, 0, 6);
            }
            if (followed) {
                gfx.lineStyle(1.5, 0x66fcf1, 0.95);
                gfx.drawCircle(0, 0, 9);
            }
            if (isInDesperation) {
                gfx.lineStyle(1, 0xef4444, 0.9);
                gfx.drawCircle(0, 0, 7);
            }
            if (onMission) {
                gfx.lineStyle(1, 0xfbbf24, 0.85);
                gfx.drawCircle(0, 0, 8);
            }
            gfx.lineStyle(0);
            gfx.beginFill(color, 0.9);
            gfx.drawCircle(0, 0, ent.type === 'mutant' ? 3 : 4);
            gfx.endFill();
            label.visible = false;
        } else if (zoom < LOD_SPRITE) {
            if (selected) {
                gfx.lineStyle(2, 0xffffff, 1.0);
                gfx.drawCircle(0, 0, 9);
            }
            if (followed) {
                gfx.lineStyle(2, 0x66fcf1, 0.95);
                gfx.drawCircle(0, 0, 12);
            }
            if (isInDesperation) {
                gfx.lineStyle(1.5, 0xef4444, 0.9);
                gfx.drawCircle(0, 0, 10);
            }
            if (onMission) {
                gfx.lineStyle(1.5, 0xfbbf24, 0.9);
                gfx.drawCircle(0, 0, 11);
            }
            gfx.lineStyle(0);
            gfx.beginFill(color, 0.95);
            if (ent.type === 'mutant') {
                gfx.drawPolygon([-5, 0, 0, -5, 5, 0, 0, 5]);
            } else {
                gfx.drawCircle(0, 0, 5);
            }
            gfx.endFill();

            label.text    = ent.name?.split("'")[1] ?? ent.name?.split(' ')[0] ?? '';
            label.visible = true;
            label.position.set(x + 7, y - 4);
        } else {
            // Pixel sprite tier
            const tex = spriteForEntity(ent);
            if (tex) {
                sprite.texture = tex;
                sprite.width = 32;
                sprite.height = 32;
                sprite.visible = true;
            } else {
                gfx.beginFill(color, 0.95);
                gfx.drawRect(-8, -8, 16, 16);
                gfx.endFill();
            }

            if (selected) {
                gfx.lineStyle(2, 0xffffff, 0.9);
                gfx.drawRect(-10, -10, 20, 20);
            }
            if (followed) {
                gfx.lineStyle(2, 0x66fcf1, 0.95);
                gfx.drawRect(-13, -13, 26, 26);
            }
            if (isInDesperation) {
                gfx.lineStyle(1.5, 0xef4444, 0.9);
                gfx.drawRect(-11, -11, 22, 22);
            }
            if (onMission) {
                gfx.lineStyle(1.5, 0xfbbf24, 0.9);
                gfx.drawRect(-12, -12, 24, 24);
            }

            if (zoom >= LOD_PAPERDOLL && ent.type === 'stalker') {
                paperdoll.visible = true;
                if (paperdollWeaponTex && ent.equipment?.weaponId) {
                    const w = new PIXI.Sprite(paperdollWeaponTex);
                    w.width = 8; w.height = 8;
                    w.position.set(10, -6);
                    paperdoll.addChild(w);
                }
                if (paperdollArmorTex && ent.equipment?.armorId) {
                    const a = new PIXI.Sprite(paperdollArmorTex);
                    a.width = 8; a.height = 8;
                    a.position.set(-14, -6);
                    const isGamma = ent.equipment.armorId.startsWith('out_');
                    a.tint = isGamma ? 0x66fcf1 : 0xffffff;
                    paperdoll.addChild(a);
                }
                if (ent.equipment?.helmetId) {
                    const h = new PIXI.Graphics();
                    h.beginFill(ent.equipment.helmetId.startsWith('helm_') ? 0xfbbf24 : 0x888888, 0.9);
                    h.drawRect(-2, -14, 6, 4);
                    h.endFill();
                    paperdoll.addChild(h);
                }
            }

            label.text    = ent.name?.split("'")[1] ?? ent.name?.split(' ')[0] ?? '';
            label.visible = true;
            label.position.set(x + 10, y - 6);
        }
    });
}

// ─── LOD Reconcile ────────────────────────────────────────────────────────────
function updateLOD() {
    const zoom = viewport.scale.x;
    labelContainer.visible = zoom >= LOD_ICON;
    if (_lastFrame) updateEntities(_lastFrame);
}

// ─── WebSocket ────────────────────────────────────────────────────────────────
let ws;
let _onStatusChange = null;
let _onMissionStats = null;
export function setStatusCallback(fn) { _onStatusChange = fn; }
export function setMissionStatsCallback(fn) { _onMissionStats = fn; }

function connectWebSocket() {
    ws = new WebSocket(WS_URL);

    ws.onopen = () => {
        if (_onStatusChange) _onStatusChange('connected');
    };

    ws.onclose = () => {
        if (_onStatusChange) _onStatusChange('disconnected');
        setTimeout(connectWebSocket, 3000);
    };

    ws.onerror = () => {
        if (_onStatusChange) _onStatusChange('error');
    };

    ws.onmessage = (e) => {
        try {
            const msg = JSON.parse(e.data);
            if (msg.type === 'inspector' && msg.data) {
                if (_onInspectorData) _onInspectorData(msg.data);
                return;
            }
            updateEntities(msg);
        } catch (err) {
            console.error('[App] Frame parse error:', err);
        }
    };
}

// ─── Layer Toggle ─────────────────────────────────────────────────────────────
export function setLayer(layer) {
    activeLayer = layer;
    if (_lastFrame) updateEntities(_lastFrame);
}

// ─── Coordinate Helpers ───────────────────────────────────────────────────────
function wX(x) { return x; }
function wY(y) { return y; }
