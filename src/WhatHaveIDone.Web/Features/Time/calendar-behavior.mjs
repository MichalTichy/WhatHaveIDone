const hour = 60 * 60 * 1000;

export function shortGapAt(at, intervals, dayStart, dayEnd) {
    let previous = null;
    for (const interval of intervals.filter(i => i.end > i.start && i.end > dayStart && i.start < dayEnd).sort((a, b) => a.start - b.start)) {
        const start = Math.max(interval.start, dayStart), end = Math.min(interval.end, dayEnd);
        if (start <= at && at < end) return null;
        if (end <= at) previous = Math.max(previous ?? end, end);
        else if (start > at) return previous !== null && start > previous && start - previous < hour ? {start: previous, end: start} : null;
    }
    return null;
}

export function snapToNeighbors(start, end, intervals, mode = 'move', threshold = 10 * 60 * 1000) {
    const candidates = [];
    for (const neighbor of intervals.filter(i => i.end > i.start)) {
        if (mode !== 'resizeEnd') candidates.push(neighbor.end - start);
        if (mode !== 'resizeStart') candidates.push(neighbor.start - end);
    }
    for (const delta of candidates.filter(value => Math.abs(value) <= threshold).sort((a, b) => Math.abs(a) - Math.abs(b))) {
        const nextStart = mode === 'resizeEnd' ? start : start + delta;
        const nextEnd = mode === 'resizeStart' ? end : end + delta;
        if (nextEnd > nextStart) return {start: nextStart, end: nextEnd};
    }
    return {start, end};
}
