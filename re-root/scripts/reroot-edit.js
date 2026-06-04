const fs = require('fs');
const f = 'c:/Users/Lionel/Desktop/kspmod/KSP-SteamInputPlugin/sauvegarde rapide #5 - reroot.sfs';
let content = fs.readFileSync(f, 'utf8');
const lines = content.split('\n');

// edits by 1-based line number; assert old (trimmed) before replacing, preserve indentation + CR
const edits = [
  // VESSEL header (dockingport)
  { ln: 3201, old: 'root = 0', neu: 'root = 1' },
  { ln: 3207, old: 'rot = 0.990851343,-0.00260308804,0.134930655,-0.000851771678', neu: 'rot = 0.4296877934616191,0.4262330235464572,0.562015644133715,0.5637667334134425' },
  { ln: 3208, old: 'CoM = -5.20218164E-05,-0.584943354,0.0412629507', neu: 'CoM = -0.0189442057,-0.0259035137,-5.29177487E-06' },
  // dockingPort3 (index 0) -> child of fuelTank, geometry relative to fuelTank
  { ln: 3243, old: 'parent = 0', neu: 'parent = 1' },
  { ln: 3244, old: 'position = 0,0,0', neu: 'position = -0.60406136512756348,0.015934944152832031,-1.862645149230957E-09' },
  { ln: 3245, old: 'rotation = 0,0,0,1', neu: 'rotation = 0.5,-0.500000119,0.5,0.5' },
  // fuelTank (index 1) -> new root
  { ln: 3475, old: 'parent = 0', neu: 'parent = 1' },
  { ln: 3476, old: 'position = -1.1920928955078125E-07,-0.60406166315078735,0.015934884548187256', neu: 'position = 0,0,0' },
  { ln: 3477, old: 'rotation = -0.50000006,0.500000298,-0.500000119,0.50000006', neu: 'rotation = 0,0,0,1' },
  // probeCore (index 2) parent unchanged (1), only reframe
  { ln: 3662, old: 'position = -8.9406967163085938E-08,-0.60406160354614258,0.38949680328369141', neu: 'position = 0,-0.37356185913085938,2.9802322387695313E-08' },
  { ln: 3663, old: 'rotation = -0.50000006,0.500000298,-0.500000119,0.50000006', neu: 'rotation = 0,0,0,1' },
  // longAntenna (index 3) parent unchanged (2), only reframe
  { ln: 4671, old: 'position = -1.1920928955078125E-07,-0.60406172275543213,0.45055961608886719', neu: 'position = 0,-0.43462467193603516,2.9802322387695313E-08' },
  { ln: 4672, old: 'rotation = 0.50000006,-0.500000119,-0.500000298,0.50000006', neu: 'rotation = 1,0,0,0' },
  // batteryPack cid 4293832580 (index 4) parent unchanged (1), only reframe
  { ln: 4865, old: 'position = 0.60372155904769897,-0.60406160354614258,0.037206590175628662', neu: 'position = 0,-0.021271705627441406,0.60372161865234375' },
  { ln: 4866, old: 'rotation = -0.5,0.500000119,0.500000298,-0.5', neu: 'rotation = -1,0,0,5.96046448E-08' },
  // batteryPack cid 4293831802 (index 5) parent unchanged (1), only reframe
  { ln: 5012, old: 'position = -0.60372179746627808,-0.60406166315078735,0.037206590175628662', neu: 'position = -5.2852556109428406E-08,-0.021271705627441406,-0.60372161865234375' },
  { ln: 5013, old: 'rotation = 0.500000238,0.500000119,0.500000119,0.50000006', neu: 'rotation = 4.37113883E-08,5.96046448E-08,1,-2.60540177E-15' },
];

for (const e of edits) {
  const i = e.ln - 1;
  const line = lines[i];
  const cr = line.endsWith('\r') ? '\r' : '';
  const body = cr ? line.slice(0, -1) : line;
  const m = body.match(/^(\s*)(.*)$/);
  const indent = m[1], val = m[2];
  if (val !== e.old) {
    throw new Error(`Line ${e.ln}: expected "${e.old}" but found "${val}"`);
  }
  lines[i] = indent + e.neu + cr;
}

fs.writeFileSync(f, lines.join('\n'));
console.log('All ' + edits.length + ' edits applied OK.');
