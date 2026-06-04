const fs = require('fs');
const f = 'c:/Users/Lionel/Desktop/kspmod/KSP-SteamInputPlugin/sauvegarde rapide #5 - reroot.sfs';
let lines = fs.readFileSync(f, 'utf8').split('\n');
const trim = s => s.replace(/\r$/, '').trim();
const crOf = s => (s.endsWith('\r') ? '\r' : '');
const indentOf = s => s.match(/^(\s*)/)[1];

// locate the "dockingport" VESSEL
const nameIdx = lines.findIndex(l => trim(l) === 'name = dockingport');
if (nameIdx < 0) throw new Error('dockingport vessel not found');
let vStart = nameIdx; while (trim(lines[vStart]) !== 'VESSEL') vStart--;
let open = vStart + 1; while (trim(lines[open]) !== '{') open++;
let depth = 0, vEnd = -1;
for (let j = open; j < lines.length; j++) {
  const t = trim(lines[j]);
  if (t === '{') depth++;
  else if (t === '}') { depth--; if (depth === 0) { vEnd = j; break; } }
}
if (vEnd < 0) throw new Error('vessel end not found');

// find immediate PART blocks
const parts = [];
for (let j = open; j <= vEnd; j++) {
  if (trim(lines[j]) === 'PART' && trim(lines[j + 1]) === '{') {
    let pd = 0, pEnd = -1;
    for (let k = j + 1; k <= vEnd; k++) {
      const tt = trim(lines[k]);
      if (tt === '{') pd++;
      else if (tt === '}') { pd--; if (pd === 0) { pEnd = k; break; } }
    }
    let uid = null;
    for (let k = j; k <= pEnd; k++) { const m = trim(lines[k]).match(/^uid = (\d+)/); if (m) { uid = m[1]; break; } }
    parts.push({ pStart: j, pEnd, uid });
    j = pEnd;
  }
}
if (parts.length !== 6) throw new Error('expected 6 parts, got ' + parts.length);
// assert current order: 0 = dockingPort3 (131499844), 1 = fuelTank (429036859)
if (parts[0].uid !== '131499844') throw new Error('part0 not dockingPort3: ' + parts[0].uid);
if (parts[1].uid !== '429036859') throw new Error('part1 not fuelTank: ' + parts[1].uid);
// contiguity
for (let k = 0; k < parts.length - 1; k++)
  if (parts[k].pEnd + 1 !== parts[k + 1].pStart) throw new Error('parts not contiguous at ' + k);

// index remap: swap 0<->1, identity for rest
const map = [1, 0, 2, 3, 4, 5];
const rm = n => (n >= 0 && n < map.length ? map[n] : n);

// remap index references within the vessel only
for (let j = vStart; j <= vEnd; j++) {
  const t = trim(lines[j]); const ind = indentOf(lines[j]); const cr = crOf(lines[j]);
  let nt = t, m;
  if ((m = t.match(/^root = (\d+)$/))) nt = 'root = ' + rm(+m[1]);
  else if ((m = t.match(/^parent = (\d+)$/))) nt = 'parent = ' + rm(+m[1]);
  else if ((m = t.match(/^sym = (\d+)$/))) nt = 'sym = ' + rm(+m[1]);
  else if ((m = t.match(/^attN = (.+), (-?\d+)$/))) nt = 'attN = ' + m[1] + ', ' + rm(+m[2]);
  else if ((m = t.match(/^srfN = srfAttach, (-?\d+)(.*)$/))) nt = 'srfN = srfAttach, ' + rm(+m[1]) + m[2];
  if (nt !== t) lines[j] = ind + nt + cr;
}

// physically reorder the part blocks: new order [fuelTank, dockingPort3, probe, antenna, bat, bat]
const blockLines = p => lines.slice(p.pStart, p.pEnd + 1);
const reordered = [parts[1], parts[0], parts[2], parts[3], parts[4], parts[5]];
const before = lines.slice(0, parts[0].pStart);
const after = lines.slice(parts[5].pEnd + 1);
const middle = [].concat(...reordered.map(blockLines));
lines = before.concat(middle, after);

fs.writeFileSync(f, lines.join('\n'));
console.log('Reorder done. Parts now:', reordered.map(p => p.uid).join(', '));
