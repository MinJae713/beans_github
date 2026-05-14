/* MVPArena map variants — game screen + tile library */

/* ─────────────────────────────────────────────
   GAME SCREEN — shows ~3 sections (S2 | S3 | S4)
   1280×720, 3-tier vertical (1F floor + 1.5F platforms + 3F safe band)
   ───────────────────────────────────────────── */
function MapScreen({variant}) {
  const cfg = MV[variant];
  const W = 1280, H = 720;

  const Y_HUD = 50;
  const Y_SAFE_TOP = 110;
  const Y_SAFE_BOT = 150;
  const Y_FLOOR_TOP = 540;
  const Y_FLOOR_BOT = 612;
  const Y_BOTTOM = 700;

  const G = 24;
  const SEC_W = (W - G*4) / 3;
  const sx = (i) => G + i*(SEC_W + G);

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" height="100%" style={{display:'block'}}>
      <MapDefs/>
      <MapSky w={W} h={H} cfg={cfg}/>

      <g opacity={cfg.starOpacity}>
        <MapStars w={W} h={H*0.7} count={120} seed={variant.charCodeAt(0)*7} color={cfg.starColor} sparkles={cfg.sparkles}/>
      </g>

      <g opacity="0.35">
        <ellipse cx={W*0.65} cy={H*0.40} rx={W*0.5} ry={110} fill={cfg.planetA.glow} filter="url(#m-bigglow)"/>
        <ellipse cx={W*0.18} cy={H*0.28} rx={W*0.28} ry={70} fill={cfg.planetB.glow} filter="url(#m-bigglow)" opacity={0.6}/>
      </g>

      <MapPlanet cx={W*0.82} cy={H*0.34} r={120} {...cfg.planetA} ring={true} glowSize={cfg.glow*1.3 + 0.2}/>
      <MapPlanet cx={W*0.18} cy={H*0.20} r={42} {...cfg.planetB} glowSize={cfg.glow*1.2 + 0.2}/>
      <MapPlanet cx={W*0.46} cy={H*0.12} r={14} {...cfg.planetC} glowSize={cfg.glow*1.1 + 0.2}/>

      <g opacity="0.45">
        <rect x={W*0.04} y={H*0.62} width="80" height="38" fill={cfg.bg[cfg.bg.length-1]}/>
        <rect x={W*0.04+24} y={H*0.55} width="32" height="14" fill={cfg.bg[cfg.bg.length-1]}/>
        <rect x={W*0.7} y={H*0.62} width="120" height="44" fill={cfg.bg[cfg.bg.length-1]}/>
        <line x1={W*0.7+16} y1={H*0.62} x2={W*0.7+16} y2={H*0.55} stroke={cfg.bg[cfg.bg.length-1]} strokeWidth="3"/>
        <line x1={W*0.7+88} y1={H*0.62} x2={W*0.7+88} y2={H*0.50} stroke={cfg.bg[cfg.bg.length-1]} strokeWidth="3"/>
      </g>

      <g opacity="0.5">
        <rect x={20} y={Y_SAFE_TOP} width={W-40} height={Y_SAFE_BOT-Y_SAFE_TOP} fill="none"
          stroke={cfg.terrain.accent} strokeWidth="1" strokeDasharray="6 6"/>
        <text x={32} y={Y_SAFE_TOP+24} fill={cfg.terrain.accent}
          fontFamily="'JetBrains Mono', monospace" fontSize="10" fontWeight="700" letterSpacing="2.5">3F · SAFE ZONE (out of jump range)</text>
      </g>

      <g opacity="0.75">
        <g transform={`translate(${W*0.32} ${H*0.30}) rotate(15)`}>
          <rect x="-10" y="-3" width="20" height="6" fill={cfg.terrain.metal} stroke={INK} strokeWidth="1.8"/>
        </g>
        <g transform={`translate(${W*0.78} ${H*0.50}) rotate(-12)`}>
          <rect x="-8" y="-3" width="16" height="6" fill={cfg.terrain.metal} stroke={INK} strokeWidth="1.8"/>
        </g>
      </g>

      {/* S2 — split floor + center void */}
      <g transform={`translate(${sx(0)} 0)`}>
        <g transform={`translate(0 ${Y_FLOOR_TOP})`}>
          <rect x={0} y={0} width={SEC_W*0.42} height={Y_FLOOR_BOT-Y_FLOOR_TOP} rx={cfg.cornerR} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
          <rect x={6} y={4} width={SEC_W*0.42-12} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
          <g transform={`translate(${SEC_W*0.58} 0)`}>
            <rect x={0} y={0} width={SEC_W*0.42} height={Y_FLOOR_BOT-Y_FLOOR_TOP} rx={cfg.cornerR} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
            <rect x={6} y={4} width={SEC_W*0.42-12} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
          </g>
        </g>
        <g transform={`translate(${SEC_W*0.42} ${Y_FLOOR_TOP+8})`}>
          {[0,1,2,3].map(i=>(
            <path key={i} d={`M ${4+i*14} 0 L ${10+i*14} 8 L ${16+i*14} 0`}
              fill="none" stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*1.0} strokeLinecap="round" opacity={0.95-i*0.12}/>
          ))}
          <text x={SEC_W*0.08} y={36} textAnchor="middle" fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="9" letterSpacing="2" fontWeight="700">VOID ↓</text>
        </g>
        <g transform={`translate(${SEC_W*0.06} ${Y_FLOOR_TOP-100})`}>
          <TileStep w={SEC_W*0.34} h={28} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.60} ${Y_FLOOR_TOP-100})`}>
          <TileStep w={SEC_W*0.34} h={28} cfg={cfg}/>
        </g>
        <SpawnDot x={SEC_W*0.18} y={Y_FLOOR_TOP-26} color="#7EC93C" label="P1"/>
        <g transform={`translate(${SEC_W/2} ${Y_BOTTOM-20})`}>
          <rect x={-46} y={-14} width="92" height="20" rx="2" fill="#0a0518" stroke={cfg.terrain.accent} strokeWidth="1.5"/>
          <text x={0} y={0} textAnchor="middle" fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="10" fontWeight="700" letterSpacing="2.5">S02 · SPAN</text>
        </g>
      </g>

      <line x1={sx(1)-G/2} y1={Y_SAFE_BOT} x2={sx(1)-G/2} y2={Y_BOTTOM-30} stroke={cfg.terrain.accent} strokeWidth="1" strokeDasharray="2 5" opacity="0.35"/>

      {/* S3 — ziggurat + reflect + blocker */}
      <g transform={`translate(${sx(1)} 0)`}>
        <g transform={`translate(0 ${Y_FLOOR_TOP})`}>
          <rect x={0} y={0} width={SEC_W} height={Y_FLOOR_BOT-Y_FLOOR_TOP} rx={cfg.cornerR} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
          <rect x={6} y={4} width={SEC_W-12} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
          <g transform={`translate(${SEC_W*0.36} ${Y_FLOOR_BOT-Y_FLOOR_TOP-9})`}>
            <rect x={0} y={0} width={SEC_W*0.28} height={5} fill={cfg.terrain.accent}/>
            {Array.from({length: 6}).map((_,i)=>(<rect key={i} x={2+i*(SEC_W*0.28/6)} y={0} width={SEC_W*0.28/12} height={5} fill={INK}/>))}
          </g>
        </g>
        <g transform={`translate(${SEC_W*0.5} ${Y_FLOOR_TOP})`}>
          <rect x={-110} y={-44} width={220} height={44} rx={cfg.cornerR} fill={cfg.terrain.c3} stroke={INK} strokeWidth={cfg.stroke}/>
          <rect x={-104} y={-40} width={208} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
          <rect x={-72} y={-86} width={144} height={42} rx={cfg.cornerR} fill={cfg.terrain.c3} stroke={INK} strokeWidth={cfg.stroke}/>
          <rect x={-66} y={-82} width={132} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
        </g>
        <g transform={`translate(${SEC_W*0.84} ${Y_FLOOR_TOP-180})`}>
          <TileReflect w={48} h={180} cfg={cfg}/>
        </g>
        {/* movable crate stack on left */}
        <g transform={`translate(${SEC_W*0.08} ${Y_FLOOR_TOP-56})`}>
          <TileBox s={56} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.08+12} ${Y_FLOOR_TOP-110})`}>
          <TileBox s={44} cfg={cfg}/>
        </g>
        {/* incline ramp connecting 1F → 1.5F */}
        <g transform={`translate(${SEC_W*0.66} ${Y_FLOOR_TOP-50})`}>
          <TileRamp w={50} h={50} cfg={cfg} dir="left"/>
        </g>
        <g transform={`translate(${SEC_W*0.32} ${Y_FLOOR_TOP-180})`}>
          <TileStep w={SEC_W*0.36} h={26} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W/2} ${Y_BOTTOM-20})`}>
          <rect x={-58} y={-14} width="116" height="20" rx="2" fill="#0a0518" stroke={cfg.terrain.accent} strokeWidth="1.5"/>
          <text x={0} y={0} textAnchor="middle" fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="10" fontWeight="700" letterSpacing="2.5">S03 · ZIGGURAT</text>
        </g>
      </g>

      <line x1={sx(2)-G/2} y1={Y_SAFE_BOT} x2={sx(2)-G/2} y2={Y_BOTTOM-30} stroke={cfg.terrain.accent} strokeWidth="1" strokeDasharray="2 5" opacity="0.35"/>

      {/* S4 — staggered ledges + tall walls + side void */}
      <g transform={`translate(${sx(2)} 0)`}>
        <g transform={`translate(0 ${Y_FLOOR_TOP})`}>
          <rect x={0} y={0} width={SEC_W*0.78} height={Y_FLOOR_BOT-Y_FLOOR_TOP} rx={cfg.cornerR} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
          <rect x={6} y={4} width={SEC_W*0.78-12} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
        </g>
        <g transform={`translate(${SEC_W*0.82} ${Y_FLOOR_TOP+8})`}>
          {[0,1,2].map(i=>(
            <path key={i} d={`M ${4+i*16} 0 L ${12+i*16} 10 L ${20+i*16} 0`}
              fill="none" stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*1.0} strokeLinecap="round" opacity={0.9-i*0.15}/>
          ))}
          <text x={28} y={36} textAnchor="middle" fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="9" letterSpacing="2" fontWeight="700">↓</text>
        </g>
        <g transform={`translate(${SEC_W*0.04} ${Y_FLOOR_TOP-94})`}>
          <TileWall w={36} h={94} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.42} ${Y_FLOOR_TOP-176})`}>
          <TileWall w={36} h={176} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.16} ${Y_FLOOR_TOP-66})`}>
          <TileStep w={SEC_W*0.22} h={24} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.50} ${Y_FLOOR_TOP-118})`}>
          <TileStep w={SEC_W*0.24} h={24} cfg={cfg}/>
        </g>
        <g transform={`translate(${SEC_W*0.10} ${Y_FLOOR_TOP-22})`}>
          <TilePipe w={84} h={22} cfg={cfg}/>
        </g>
        <SpawnDot x={SEC_W*0.62} y={Y_FLOOR_TOP-26} color="#E96B7E" label="P2"/>
        <g transform={`translate(${SEC_W/2} ${Y_BOTTOM-20})`}>
          <rect x={-58} y={-14} width="116" height="20" rx="2" fill="#0a0518" stroke={cfg.terrain.accent} strokeWidth="1.5"/>
          <text x={0} y={0} textAnchor="middle" fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="10" fontWeight="700" letterSpacing="2.5">S04 · LEDGE</text>
        </g>
      </g>

      {/* HUD strip */}
      <g>
        <rect x={0} y={0} width={W} height={Y_HUD} fill="#0e0820" opacity="0.55"/>
        <line x1={0} y1={Y_HUD} x2={W} y2={Y_HUD} stroke={cfg.terrain.accent} strokeWidth="1" opacity="0.4"/>
        <text x={24} y={20} fill="#fff" fontFamily="'Outfit', sans-serif" fontSize="13" fontWeight="700" letterSpacing="1.5">MVPARENA</text>
        <text x={24} y={36} fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="9" letterSpacing="2.5">MAP CONCEPT · {cfg.name}</text>
        <g transform={`translate(${W/2 - 90} 18)`}>
          {Array.from({length: 7}).map((_,i)=>{
            const isVR = i===0 || i===6;
            const visible = i>=1 && i<=3;
            return (
              <g key={i} transform={`translate(${i*26} 0)`}>
                <rect x={0} y={0} width="22" height="14" rx="2"
                  fill={visible ? cfg.terrain.accent : (isVR? '#7BC9B4' : '#fff')}
                  opacity={visible ? 1 : 0.30}/>
                <text x={11} y={11} textAnchor="middle"
                  fill={visible ? INK : '#fff'} opacity={visible? 1: 0.6}
                  fontFamily="'JetBrains Mono', monospace" fontSize="8" fontWeight="700">S{i+1}</text>
              </g>
            );
          })}
          <text x={91} y={30} textAnchor="middle" fill="#fff" opacity="0.55" fontFamily="'JetBrains Mono', monospace" fontSize="8" letterSpacing="2">VIEWING S2-S4</text>
        </g>
        <g transform={`translate(${W-24} 14)`} fontFamily="'JetBrains Mono', monospace" fontSize="9" letterSpacing="1.5">
          <text x={0} y={8} textAnchor="end" fill="#7BC9B4">3F · SAFE   1.5F · STEP   1F · FLOOR</text>
          <text x={0} y={26} textAnchor="end" fill={cfg.terrain.accent}>JUMP MAX = 1.5F</text>
        </g>
      </g>

      <MapGrain w={W} h={H} opacity={cfg.grain} seed={7}/>
    </svg>
  );
}

/* TILE LIBRARY — 8 props in 4×2 grid */
function MapTiles({variant}) {
  const cfg = MV[variant];
  const W = 1080, H = 600;
  const cols = 4;
  const cw = 240, ch = 220, gap = 16;
  const startX = (W - (cw*cols + gap*(cols-1)))/2;
  const startY = 110;

  // Each item gives its own (tw, th); card renders centered.
  const center = (tw, th, node) => <g transform={`translate(${-tw/2} ${-th/2})`}>{node}</g>;
  const cards = [
    { label:'Floor Base',         code:'T_FLR · 1F',   el: center(220, 56,  <TileFloor w={220} h={56} cfg={cfg}/>) },
    { label:'Wall Material',      code:'T_WAL',        el: center(56,  150, <TileWall w={56} h={150} cfg={cfg}/>) },
    { label:'Step Block',         code:'T_STP · 1.5F', el: center(180, 32,  <TileStep w={180} h={32} cfg={cfg}/>) },
    { label:'Ramp 45°',           code:'T_RMP',        el: center(120, 120, <TileRamp w={120} h={120} cfg={cfg} dir="right"/>) },
    { label:'Reflective Surface', code:'T_RFL',        el: center(52,  150, <TileReflect w={52} h={150} cfg={cfg}/>) },
    { label:'Fall Signal',        code:'T_VOID · ↓',   el: center(220, 56,  <TileVoid w={220} h={56} cfg={cfg}/>) },
    { label:'Movable Crate',      code:'P_BOX',        el: center(110, 110, <TileBox s={110} cfg={cfg}/>) },
    { label:'Pipe Debris',        code:'P_PIP',        el: center(176, 50,  <TilePipe w={176} h={50} cfg={cfg}/>) },
  ];

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" height="100%" style={{display:'block'}}>
      <MapDefs/>
      <defs>
        <linearGradient id="tlbg2" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={cfg.bg[Math.max(0, cfg.bg.length-2)]}/>
          <stop offset="1" stopColor={cfg.bg[cfg.bg.length-1]}/>
        </linearGradient>
      </defs>
      <rect width={W} height={H} fill="url(#tlbg2)"/>
      <g opacity="0.4"><MapStars w={W} h={H} count={36} seed={variant.charCodeAt(0)*9} color={cfg.starColor}/></g>

      <g transform="translate(28 40)">
        <text x="0" y="0" fill="#fff" fontFamily="'Outfit', sans-serif" fontSize="22" fontWeight="700">TILE LIBRARY · 8 PRIMITIVES</text>
        <text x="0" y="22" fill="#fff" opacity="0.6" fontFamily="'JetBrains Mono', monospace" fontSize="10" letterSpacing="2">S2–S6 LAYOUTS COMPOSE FROM THESE — {cfg.name}</text>
      </g>

      {cards.map((c, i) => {
        const col = i % cols, row = Math.floor(i / cols);
        return (
          <g key={i} transform={`translate(${startX + col*(cw+gap)} ${startY + row*(ch+gap)})`}>
            <TileCard label={c.label} code={c.code} w={cw} h={ch} cfg={cfg}>
              {c.el}
            </TileCard>
          </g>
        );
      })}

      <g transform={`translate(28 ${H-32})`}>
        <text x="0" y="0" fill="#fff" opacity="0.55" fontFamily="'JetBrains Mono', monospace" fontSize="9" letterSpacing="2">
          STRUCTURE · T_FLR T_WAL T_STP T_RMP T_RFL T_VOID    INTERACTIVE · P_BOX    SCATTER · P_PIP
        </text>
      </g>

      <MapGrain w={W} h={H} opacity={cfg.grain*0.7} seed={3}/>
    </svg>
  );
}

Object.assign(window, { MapScreen, MapTiles });
