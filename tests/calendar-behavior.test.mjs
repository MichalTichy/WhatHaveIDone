import {test} from 'node:test';
import assert from 'node:assert/strict';
import {shortGapAt,snapToNeighbors} from '../src/WhatHaveIDone.Web/Scripts/calendar-behavior.mjs';
const minute=60*1000;
const interval=(start,end)=>({start:start*minute,end:end*minute});

test('click fills only a bounded gap shorter than an hour and unions overlapping entries',()=>{
    const entries=[interval(600,630),interval(540,610),interval(650,700)];
    assert.deepEqual(shortGapAt(640*minute,entries,0,1440*minute),interval(630,650));
    assert.equal(shortGapAt(620*minute,entries,0,1440*minute),null);
    assert.equal(shortGapAt(500*minute,entries,0,1440*minute),null);
    assert.equal(shortGapAt(720*minute,entries,0,1440*minute),null);
    assert.equal(shortGapAt(640*minute,[interval(540,600),interval(660,700)],0,1440*minute),null);
});
test('gap boundaries use actual instants on a 25-hour day and clip cross-midnight entries',()=>{
    const start=Date.parse('2026-10-25T00:00:00+02:00'),end=Date.parse('2026-10-26T00:00:00+01:00');
    const a=Date.parse('2026-10-25T02:50:00+02:00'),b=Date.parse('2026-10-25T02:10:00+01:00');
    assert.equal(end-start,25*60*minute);
    assert.deepEqual(shortGapAt(a+5*minute,[{start:start-minute,end:a},{start:b,end:end+minute}],start,end),{start:a,end:b});
});
test('move snaps to either neighbor, keeps duration and requires a deliberate overlap beyond ten minutes',()=>{
    assert.deepEqual(snapToNeighbors(605*minute,635*minute,[interval(540,600)]),interval(600,630));
    assert.deepEqual(snapToNeighbors(590*minute,620*minute,[interval(540,600)]),interval(600,630));
    assert.deepEqual(snapToNeighbors(585*minute,615*minute,[interval(540,600)]),interval(585,615));
    assert.deepEqual(snapToNeighbors(565*minute,595*minute,[interval(600,660)]),interval(570,600));
});
test('resize snaps only the edited edge and never creates a nonpositive interval',()=>{
    assert.deepEqual(snapToNeighbors(570*minute,605*minute,[interval(600,660)],'resizeEnd'),interval(570,600));
    assert.deepEqual(snapToNeighbors(595*minute,650*minute,[interval(540,600)],'resizeStart'),interval(600,650));
    assert.deepEqual(snapToNeighbors(600*minute,605*minute,[interval(600,660)],'resizeEnd'),interval(600,605));
});
