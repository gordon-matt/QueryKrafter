// QueryKrafter browser helpers. Loaded as a module by the component on first use.

const ZONE = '.qk-drop-zone';
const ACTIVE = 'qk-drop-active';

// Drag feedback is handled here rather than in .NET: dragover fires continuously, and a round
// trip per event would flood a Blazor Server circuit. .NET only hears about the start and the drop.
export function initDragDrop(root) {
    if (!root || root.__qkDragDrop) {
        return;
    }

    root.__qkDragDrop = true;
    let current = null;

    const setCurrent = zone => {
        if (current === zone) {
            return;
        }

        current?.classList.remove(ACTIVE);
        current = zone;
        current?.classList.add(ACTIVE);
    };

    root.addEventListener('dragstart', e => {
        const handle = e.target.closest?.('.qk-handle');
        if (!handle || !root.contains(handle)) {
            return;
        }

        if (e.dataTransfer) {
            // Firefox won't start a drag without data.
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', 'querykrafter');

            // Drag the whole rule or group, not just the handle.
            const item = handle.closest('.qk-rule, .qk-group');
            if (item) {
                const box = item.getBoundingClientRect();
                const handleBox = handle.getBoundingClientRect();
                e.dataTransfer.setDragImage(item, handleBox.left - box.left + 8, handleBox.top - box.top + 8);
            }
        }

        root.classList.add('qk-dragging');
    });

    root.addEventListener('dragover', e => {
        const zone = e.target.closest?.(ZONE);
        if (zone && root.contains(zone)) {
            e.preventDefault();
            if (e.dataTransfer) {
                e.dataTransfer.dropEffect = 'move';
            }

            setCurrent(zone);
        } else {
            setCurrent(null);
        }
    });

    const end = () => {
        setCurrent(null);
        root.classList.remove('qk-dragging');
    };

    root.addEventListener('drop', end);
    root.addEventListener('dragend', end);
}

// Lets plain JavaScript and React hosts react to the component, e.g. el.addEventListener('qk-changed', ...).
export function dispatch(element, eventName, detailJson) {
    if (element instanceof Element) {
        element.dispatchEvent(new CustomEvent(eventName, { bubbles: true, composed: true, detail: JSON.parse(detailJson) }));
    }
}
