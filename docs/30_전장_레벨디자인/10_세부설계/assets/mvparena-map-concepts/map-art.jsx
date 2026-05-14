/* MVPArena map — flatter, ROUNDS-leaning primitives.
   No characters. Single solid fills, thin outlines, minimal detail. */

const MV = {
  A: {
    name: 'A · ROUNDS-LEAN',
    blurb: 'Most flat, most structural. Same flat sky, but the wreckage gets a more serious treatment — desaturated rust, smaller corner radii, slightly muted accent. Less toy, more salvage.',
    bg: ['#4D78A8', '#3F2167', '#2A1B4A'],
    bgStops: [0, 0.6, 1],
    planetA: { base:'#A87FBE', highlight:'#D8B7E8', shadow:'#54376B', halo:0.3, glow:'#C8A5DC' },
    planetB: { base:'#7AC5C5', highlight:'#C8F0EE', shadow:'#3D6E6E', halo:0.25, glow:'#9CDCDC' },
    planetC: { base:'#F5C82B', highlight:'#FFE89A', shadow:'#9A7C18', halo:0.2, glow:'#FFD96A' },
    starColor: '#FFFFFF', starOpacity: 0.7, sparkles: 0,
    terrain: { c1:'#7E443F', c2:'#3E1F1C', c3:'#5C2F2B', accent:'#D9A823', metal:'#7E7E94', metalD:'#3A3A4F' },
    grain: 0.06, stroke: 2.2, glow: 0.6, cornerR: 3,
  },
  B: {
    name: 'B · ASTRONEER',
    blurb: 'Soft pastel gradient sky, rounded-minimal forms. Same structure as A, warmer corners, cream-tinted hull.',
    bg: ['#7BC1CC', '#5BA8B8', '#4D78A8', '#332256'],
    bgStops: [0, 0.35, 0.7, 1],
    planetA: { base:'#F0C2D8', highlight:'#FFE4F1', shadow:'#9B5C82', halo:0.4, glow:'#FFB7DA' },
    planetB: { base:'#A87FBE', highlight:'#D8B7E8', shadow:'#54376B', halo:0.45, glow:'#C8A5DC' },
    planetC: { base:'#7AC5C5', highlight:'#C8F0EE', shadow:'#3D6E6E', halo:0.4, glow:'#9CDCDC' },
    starColor: '#FFFFFF', starOpacity: 0.85, sparkles: 0,
    terrain: { c1:'#C97870', c2:'#7A3B36', c3:'#A05952', accent:'#F5C82B', metal:'#B0B1C7', metalD:'#5A5A75' },
    grain: 0.10, stroke: 2.5, glow: 0.8, cornerR: 10,
  },
  C: {
    name: 'C · SAYONARA',
    blurb: 'Violet-to-magenta sky, neon halo on planets and edge-lit props. Cool and synthy without crossing into cyberpunk.',
    bg: ['#3F2167', '#5A2380', '#8E2B8B', '#2A1B4A'],
    bgStops: [0, 0.45, 0.7, 1],
    planetA: { base:'#C76EBC', highlight:'#FFC8F0', shadow:'#6E2A66', halo:0.95, glow:'#FF8AE0' },
    planetB: { base:'#A87FBE', highlight:'#E0B8F0', shadow:'#3D1F58', halo:0.85, glow:'#C088E8' },
    planetC: { base:'#5BA8B8', highlight:'#A8E4EC', shadow:'#1F4858', halo:0.7, glow:'#7AC5C5' },
    starColor: '#F8DEFF', starOpacity: 0.95, sparkles: 6,
    terrain: { c1:'#8E3F66', c2:'#3A1840', c3:'#5C2754', accent:'#FF6FB8', metal:'#9087B8', metalD:'#3A3358' },
    grain: 0.06, stroke: 2.5, glow: 1.4, cornerR: 8,
  },
  D: {
    name: 'D · HYBRID',
    blurb: 'Three-way blend: ROUNDS structure, Astroneer color, Sayonara accent glow. The middle path that keeps everything legible.',
    bg: ['#5BA8B8', '#4F5BA0', '#5A2F88', '#332256'],
    bgStops: [0, 0.4, 0.75, 1],
    planetA: { base:'#A87FBE', highlight:'#E4C8F0', shadow:'#54376B', halo:0.6, glow:'#C9A0E0' },
    planetB: { base:'#7AC5C5', highlight:'#CFEBEB', shadow:'#36696D', halo:0.55, glow:'#9DDCDC' },
    planetC: { base:'#E96B7E', highlight:'#FFB6C0', shadow:'#7A2A38', halo:0.5, glow:'#FF9CB0' },
    starColor: '#FFFFFF', starOpacity: 0.9, sparkles: 3,
    terrain: { c1:'#A04E48', c2:'#5E2C28', c3:'#7A3B36', accent:'#F5C82B', metal:'#9D9CB8', metalD:'#4D4D66' },
    grain: 0.08, stroke: 2.5, glow: 1.0, cornerR: 8,
  },
};

const INK = '#1A1030';

/* ───────── filters ───────── */
function MapDefs() {
  return (
    <defs>
      <filter id="m-glow" x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="6"/>
      </filter>
      <filter id="m-bigglow" x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="22"/>
      </filter>
    </defs>
  );
}

function MapGrain({w, h, opacity=0.08, seed=4}) {
  const id = React.useId();
  return (
    <>
      <filter id={id}>
        <feTurbulence type="fractalNoise" baseFrequency="1.4" numOctaves="2" seed={seed}/>
        <feColorMatrix values={`0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 ${opacity} 0`}/>
      </filter>
      <rect width={w} height={h} filter={`url(#${id})`} style={{mixBlendMode:'overlay'}}/>
    </>
  );
}

/* sky */
function MapSky({w, h, cfg}) {
  const id = React.useId().replace(/:/g,'');
  return (
    <>
      <defs>
        <linearGradient id={`sky-${id}`} x1="0" y1="0" x2="0" y2="1">
          {cfg.bg.map((c,i)=>(<stop key={i} offset={cfg.bgStops[i]} stopColor={c}/>))}
        </linearGradient>
      </defs>
      <rect width={w} height={h} fill={`url(#sky-${id})`}/>
    </>
  );
}

/* deterministic stars */
function MapStars({w, h, count=80, seed=1, color='#fff', opacity=0.85, sparkles=0}) {
  let s = seed;
  const rnd = () => (s = (s*9301 + 49297) % 233280) / 233280;
  const dots = [];
  for (let i=0;i<count;i++) {
    const x = rnd()*w, y = rnd()*h*0.78;
    const r = 0.6 + rnd()*1.4;
    const op = (0.35 + rnd()*0.65) * opacity;
    dots.push(<circle key={i} cx={x} cy={y} r={r} fill={color} opacity={op}/>);
  }
  const sp = [];
  for (let i=0;i<sparkles;i++) {
    const x = rnd()*w, y = rnd()*h*0.7;
    const L = 5 + rnd()*7;
    sp.push(
      <g key={`s${i}`} transform={`translate(${x} ${y})`} opacity={0.6+rnd()*0.4}>
        <line x1={-L} y1={0} x2={L} y2={0} stroke={color} strokeWidth={0.7} strokeLinecap="round"/>
        <line x1={0} y1={-L} x2={0} y2={L} stroke={color} strokeWidth={0.7} strokeLinecap="round"/>
      </g>
    );
  }
  return <g>{dots}{sp}</g>;
}

/* simple flat planet */
function MapPlanet({cx, cy, r, base, highlight, shadow, halo=0.4, glow='#fff', glowSize=1.5, ring=false}) {
  const gid = `mp-${React.useId()}`.replace(/:/g,'');
  return (
    <g>
      <circle cx={cx} cy={cy} r={r*glowSize} fill={glow} opacity={halo} filter="url(#m-bigglow)"/>
      <defs>
        <radialGradient id={gid} cx="35%" cy="35%" r="75%">
          <stop offset="0%" stopColor={highlight}/>
          <stop offset="55%" stopColor={base}/>
          <stop offset="100%" stopColor={shadow}/>
        </radialGradient>
      </defs>
      <circle cx={cx} cy={cy} r={r} fill={`url(#${gid})`}/>
      {ring && (
        <g transform={`rotate(-12 ${cx} ${cy})`}>
          <ellipse cx={cx} cy={cy} rx={r*1.7} ry={r*0.32} fill="none" stroke={highlight} strokeWidth={r*0.05} opacity={0.65}/>
        </g>
      )}
    </g>
  );
}

/* ─────────────────────────────────────────────
   TILE PRIMITIVES — flat, single fill, thin outline
   All centered at (0,0) within their own group, sized to fit a parent transform.
   ───────────────────────────────────────────── */

/* T_FLR — main floor block (rectangular slab with rivets + warning stripe) */
function TileFloor({w=240, h=72, cfg, accent=true}) {
  return (
    <g>
      <rect x={0} y={0} width={w} height={h} rx={cfg.cornerR} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
      {/* top highlight */}
      <rect x={6} y={4} width={w-12} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
      {/* mid panel line */}
      <line x1={w*0.5} y1={cfg.cornerR} x2={w*0.5} y2={h-cfg.cornerR} stroke={INK} strokeWidth={cfg.stroke*0.7} opacity="0.55"/>
      {/* rivets */}
      {[0.15, 0.35, 0.65, 0.85].map((p,i)=>(
        <circle key={i} cx={w*p} cy={h*0.42} r={2.2} fill={INK} opacity="0.7"/>
      ))}
      {accent && (
        <g transform={`translate(${w*0.18} ${h-9})`}>
          <rect x={0} y={0} width={w*0.22} height={5} fill={cfg.terrain.accent}/>
          {[0,1,2,3].map(i=>(<rect key={i} x={2+i*(w*0.22/4)} y={0} width={w*0.22/8} height={5} fill={INK}/>))}
        </g>
      )}
    </g>
  );
}

/* T_WAL — wall material (tall) */
function TileWall({w=80, h=200, cfg}) {
  return (
    <g>
      <rect x={0} y={0} width={w} height={h} rx={cfg.cornerR} fill={cfg.terrain.c2} stroke={INK} strokeWidth={cfg.stroke}/>
      <rect x={w*0.5-2} y={cfg.cornerR} width={4} height={h-cfg.cornerR*2} fill={INK} opacity="0.35"/>
      {[0.15, 0.4, 0.6, 0.85].map((p,i)=>(
        <g key={i}>
          <circle cx={w*0.22} cy={h*p} r={2} fill={INK} opacity="0.75"/>
          <circle cx={w*0.78} cy={h*p} r={2} fill={INK} opacity="0.75"/>
        </g>
      ))}
    </g>
  );
}

/* T_STP — 1.5F floating step block */
function TileStep({w=180, h=44, cfg}) {
  return (
    <g>
      <rect x={0} y={0} width={w} height={h} rx={cfg.cornerR} fill={cfg.terrain.c3} stroke={INK} strokeWidth={cfg.stroke}/>
      <rect x={4} y={3} width={w-8} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
      <rect x={cfg.cornerR} y={h-8} width={w-cfg.cornerR*2} height={3} fill={INK} opacity="0.4"/>
      {/* support strut hint underneath */}
      <line x1={w*0.5} y1={h} x2={w*0.5} y2={h+10} stroke={INK} strokeWidth={cfg.stroke*0.8} opacity="0.55"/>
    </g>
  );
}

/* T_RFL — reflective surface (chrome panel with diagonal shine) */
function TileReflect({w=80, h=160, cfg}) {
  const gid = `rfl-${React.useId()}`.replace(/:/g,'');
  return (
    <g>
      <defs>
        <linearGradient id={gid} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#E8F4FF"/>
          <stop offset="40%" stopColor={cfg.terrain.metal}/>
          <stop offset="60%" stopColor="#F8FAFF"/>
          <stop offset="100%" stopColor={cfg.terrain.metalD}/>
        </linearGradient>
      </defs>
      <rect x={0} y={0} width={w} height={h} rx={4} fill={`url(#${gid})`} stroke={INK} strokeWidth={cfg.stroke}/>
      {/* diagonal shine bands */}
      <g opacity="0.5">
        <rect x={-h*0.2} y={h*0.2} width={6} height={h*1.5} fill="#fff" transform={`rotate(-30 ${w/2} ${h/2})`}/>
        <rect x={-h*0.2 + 14} y={h*0.2} width={2} height={h*1.5} fill="#fff" transform={`rotate(-30 ${w/2} ${h/2})`}/>
      </g>
      {/* corner brackets */}
      <path d={`M 4 4 L 14 4 M 4 4 L 4 14`} stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*0.8} fill="none"/>
      <path d={`M ${w-4} ${h-4} L ${w-14} ${h-4} M ${w-4} ${h-4} L ${w-4} ${h-14}`} stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*0.8} fill="none"/>
    </g>
  );
}

/* T_RMP — 45° incline ramp (right-rising by default) */
function TileRamp({w=160, h=160, cfg, dir='right'}) {
  // local origin top-left, square wedge
  const pts = dir === 'right'
    ? `0,${h} ${w},${h} ${w},0`
    : `0,${h} ${w},${h} 0,0`;
  return (
    <g>
      <polygon points={pts} fill={cfg.terrain.c1} stroke={INK} strokeWidth={cfg.stroke}/>
      {/* surface highlight along hypotenuse */}
      {dir === 'right' ? (
        <line x1={4} y1={h-3} x2={w-3} y2={4} stroke="#fff" strokeWidth="3" opacity="0.22" strokeLinecap="round"/>
      ) : (
        <line x1={3} y1={4} x2={w-4} y2={h-3} stroke="#fff" strokeWidth="3" opacity="0.22" strokeLinecap="round"/>
      )}
      {/* inner incline hatch */}
      <g opacity="0.45">
        {[0.3, 0.5, 0.7].map((p,i)=>(
          dir === 'right' ? (
            <line key={i} x1={w*p} y1={h} x2={w*p} y2={h - h*(1-p)} stroke={INK} strokeWidth={cfg.stroke*0.6}/>
          ) : (
            <line key={i} x1={w*p} y1={h} x2={w*p} y2={h*p} stroke={INK} strokeWidth={cfg.stroke*0.6}/>
          )
        ))}
      </g>
      {/* baseline rivets */}
      <circle cx={w*0.18} cy={h-6} r={2} fill={INK} opacity="0.7"/>
      <circle cx={w*0.5}  cy={h-6} r={2} fill={INK} opacity="0.7"/>
      <circle cx={w*0.82} cy={h-6} r={2} fill={INK} opacity="0.7"/>
    </g>
  );
}

/* T_VOID — fall zone signal (chevrons + hue cue) */
function TileVoid({w=240, h=70, cfg}) {
  return (
    <g>
      {/* darker drop background */}
      <rect x={0} y={0} width={w} height={h} fill={cfg.terrain.c2} opacity="0.0"/>
      {/* chevrons */}
      <g transform={`translate(${w*0.5 - 60} ${h*0.25})`}>
        {[0,1,2,3,4].map(i=>(
          <path key={i} d={`M ${i*30} 0 L ${i*30 + 12} 12 L ${i*30 + 24} 0`}
            fill="none" stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*1.1} strokeLinecap="round" opacity={0.95 - i*0.1}/>
        ))}
      </g>
      <text x={w*0.5} y={h*0.85} textAnchor="middle" fill={cfg.terrain.accent}
        fontFamily="'JetBrains Mono', monospace" fontSize="11" fontWeight="700" letterSpacing="3">VOID ↓</text>
    </g>
  );
}

/* T_PIPE — debris prop A (broken pipe segment) */
function TilePipe({w=180, h=60, cfg}) {
  return (
    <g>
      <rect x={4} y={h*0.25} width={w-8} height={h*0.5} rx={cfg.cornerR} fill={cfg.terrain.metal} stroke={INK} strokeWidth={cfg.stroke}/>
      <rect x={4} y={h*0.3} width={w-8} height={3} rx={1.5} fill="#fff" opacity="0.32"/>
      {/* flange ends */}
      <rect x={0} y={h*0.18} width={12} height={h*0.64} rx={2} fill={cfg.terrain.metalD} stroke={INK} strokeWidth={cfg.stroke}/>
      <rect x={w-12} y={h*0.18} width={12} height={h*0.64} rx={2} fill={cfg.terrain.metalD} stroke={INK} strokeWidth={cfg.stroke}/>
      {/* break crack */}
      <path d={`M ${w*0.6} ${h*0.25} L ${w*0.55} ${h*0.5} L ${w*0.62} ${h*0.75}`} stroke={INK} strokeWidth={cfg.stroke*0.7} fill="none" opacity="0.7"/>
    </g>
  );
}

/* P_BOX — movable crate (player can push or shoot) */
function TileBox({s=120, cfg}) {
  const w = s, h = s;
  return (
    <g>
      {/* outer crate */}
      <rect x={0} y={0} width={w} height={h} rx={cfg.cornerR*0.8} fill={cfg.terrain.c3} stroke={INK} strokeWidth={cfg.stroke}/>
      {/* top surface highlight */}
      <rect x={5} y={4} width={w-10} height={3} rx={1.5} fill="#fff" opacity="0.22"/>
      {/* inner panel */}
      <rect x={w*0.16} y={h*0.16} width={w*0.68} height={h*0.68} rx={cfg.cornerR*0.5}
        fill="none" stroke={INK} strokeWidth={cfg.stroke*0.7} opacity="0.55"/>
      {/* diagonal cross brace */}
      <line x1={w*0.16} y1={h*0.16} x2={w*0.84} y2={h*0.84} stroke={INK} strokeWidth={cfg.stroke*0.6} opacity="0.4"/>
      <line x1={w*0.84} y1={h*0.16} x2={w*0.16} y2={h*0.84} stroke={INK} strokeWidth={cfg.stroke*0.6} opacity="0.4"/>
      {/* corner bolts */}
      {[[0.18,0.18],[0.82,0.18],[0.18,0.82],[0.82,0.82]].map(([px,py],i)=>(
        <circle key={i} cx={w*px} cy={h*py} r={2.2} fill={INK} opacity="0.85"/>
      ))}
      {/* center accent square */}
      <rect x={w*0.42} y={h*0.42} width={w*0.16} height={h*0.16} fill={cfg.terrain.accent} stroke={INK} strokeWidth={cfg.stroke*0.6}/>
      {/* motion-arrow side cues */}
      <g opacity="0.85">
        <path d={`M ${-6} ${h*0.5} L ${-12} ${h*0.5 - 5} M ${-6} ${h*0.5} L ${-12} ${h*0.5 + 5}`}
          stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*0.8} fill="none" strokeLinecap="round"/>
        <path d={`M ${w+6} ${h*0.5} L ${w+12} ${h*0.5 - 5} M ${w+6} ${h*0.5} L ${w+12} ${h*0.5 + 5}`}
          stroke={cfg.terrain.accent} strokeWidth={cfg.stroke*0.8} fill="none" strokeLinecap="round"/>
      </g>
    </g>
  );
}

/* T_ANT — (legacy, kept exported but no longer surfaced) */
function TileAntenna({w=120, h=160, cfg}) {
  return (
    <g>
      {/* console base */}
      <rect x={w*0.25} y={h*0.7} width={w*0.5} height={h*0.3} rx={cfg.cornerR*0.6} fill={cfg.terrain.c2} stroke={INK} strokeWidth={cfg.stroke}/>
      <rect x={w*0.32} y={h*0.78} width={w*0.36} height={h*0.06} rx={2} fill={cfg.terrain.accent} opacity="0.85"/>
      <circle cx={w*0.4} cy={h*0.92} r={2.5} fill={cfg.terrain.accent}/>
      <circle cx={w*0.5} cy={h*0.92} r={2.5} fill="#fff" opacity="0.6"/>
      <circle cx={w*0.6} cy={h*0.92} r={2.5} fill={INK}/>
      {/* antenna mast */}
      <line x1={w*0.5} y1={h*0.7} x2={w*0.5} y2={h*0.05} stroke={INK} strokeWidth={cfg.stroke*1.2} strokeLinecap="round"/>
      {/* cross arms */}
      <line x1={w*0.5} y1={h*0.18} x2={w*0.18} y2={h*0.10} stroke={INK} strokeWidth={cfg.stroke} strokeLinecap="round"/>
      <line x1={w*0.5} y1={h*0.18} x2={w*0.82} y2={h*0.10} stroke={INK} strokeWidth={cfg.stroke} strokeLinecap="round"/>
      <line x1={w*0.5} y1={h*0.32} x2={w*0.28} y2={h*0.27} stroke={INK} strokeWidth={cfg.stroke} strokeLinecap="round"/>
      <line x1={w*0.5} y1={h*0.32} x2={w*0.72} y2={h*0.27} stroke={INK} strokeWidth={cfg.stroke} strokeLinecap="round"/>
      {/* beacon */}
      <circle cx={w*0.5} cy={h*0.05} r={6} fill={cfg.terrain.accent} opacity="0.55" filter="url(#m-glow)"/>
      <circle cx={w*0.5} cy={h*0.05} r={3.5} fill={cfg.terrain.accent} stroke={INK} strokeWidth={cfg.stroke*0.7}/>
    </g>
  );
}

/* spawn placeholder */
function SpawnDot({x, y, color='#7EC93C', label='P1'}) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <circle cx={0} cy={0} r={20} fill="none" stroke={color} strokeWidth="2.5" strokeDasharray="4 3" opacity="0.85"/>
      <circle cx={0} cy={0} r={12} fill={color} opacity="0.18"/>
      <circle cx={0} cy={0} r={3} fill={color}/>
      <text x={0} y={-26} textAnchor="middle" fill={color} fontFamily="'JetBrains Mono', monospace" fontSize="9" fontWeight="700" letterSpacing="2">{label}</text>
    </g>
  );
}

/* tile lib card — children placed at TRUE center of content area */
function TileCard({label, code, w=240, h=200, cfg, children}) {
  const labelH = 32;
  const padTop = 12;
  const cy = padTop + (h - labelH - padTop) / 2;
  return (
    <g>
      <rect x={0} y={0} width={w} height={h} rx={4} fill="#150a30" stroke={cfg.terrain.accent} strokeOpacity="0.22" strokeWidth="1"/>
      {/* dot grid (full content area) */}
      <g opacity="0.16">
        {Array.from({length: Math.floor(w/16)}).map((_,i)=>(
          Array.from({length: Math.floor((h-labelH)/16)}).map((_,j)=>(
            <circle key={`${i}-${j}`} cx={i*16+8} cy={j*16+padTop} r="0.7" fill="#fff"/>
          ))
        ))}
      </g>
      {/* center crosshair guide (very subtle) */}
      <line x1={w/2} y1={padTop} x2={w/2} y2={h-labelH} stroke={cfg.terrain.accent} strokeWidth="1" strokeDasharray="2 5" opacity="0.18"/>
      <g transform={`translate(${w/2} ${cy})`}>{children}</g>
      <g transform={`translate(10 ${h-12})`}>
        <text x={0} y={0} fill={cfg.terrain.accent} fontFamily="'JetBrains Mono', monospace" fontSize="8" letterSpacing="1.6">{code}</text>
      </g>
      <g transform={`translate(10 ${h-22})`}>
        <text x={0} y={0} fill="#fff" fontFamily="'Outfit', sans-serif" fontSize="11" fontWeight="700">{label}</text>
      </g>
    </g>
  );
}

Object.assign(window, {
  MV, INK, MapDefs, MapGrain, MapSky, MapStars, MapPlanet,
  TileFloor, TileWall, TileStep, TileReflect, TileVoid, TilePipe, TileAntenna,
  TileRamp, TileBox,
  SpawnDot, TileCard,
});
