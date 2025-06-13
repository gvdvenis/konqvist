// wwwroot/js/mapInterop.js

let mapDotNetRef = null;
let olMap = null;

export function registerMapDotNetRef(dotNetRef) {
    mapDotNetRef = dotNetRef;
}

export function setSelectedDistrict(featureId) {
    if (!olMap || !featureId) return false;
    const select = getOrCreateSelectInteraction(olMap);
    const feature = findFeatureById(olMap, featureId);
    if (select && feature) {
        const features = select.getFeatures();
        features.clear();
        features.push(feature);
        return true;
    }
    return false;
}

export function configureMap(map) {
    olMap = map;
    const viewport = map.getViewport();
    let touchStartPixel = null;
    const TAP_MAX_DISTANCE = 30;

    viewport.addEventListener('touchstart', e => {
        if (e.touches.length === 1) {
            const rect = viewport.getBoundingClientRect();
            touchStartPixel = [
                e.touches[0].clientX - rect.left,
                e.touches[0].clientY - rect.top
            ];
        }
    }, { passive: true });

    viewport.addEventListener('touchend', e => {
        if (touchStartPixel && e.changedTouches.length === 1) {
            const rect = viewport.getBoundingClientRect();
            const endPixel = [
                e.changedTouches[0].clientX - rect.left,
                e.changedTouches[0].clientY - rect.top
            ];
            const dx = endPixel[0] - touchStartPixel[0];
            const dy = endPixel[1] - touchStartPixel[1];
            if (Math.sqrt(dx * dx + dy * dy) < TAP_MAX_DISTANCE) {
                map.forEachFeatureAtPixel(endPixel, (feature) => {
                    if (feature?.getId && mapDotNetRef) {
                        mapDotNetRef.invokeMethodAsync('InvokeShapeClickFromJs', feature.getId());
                    }
                    return true;
                });
            }
        }
        touchStartPixel = null;
    }, { passive: true });
}

// Helpers (exported for testability/future use)
export function getOrCreateSelectInteraction(map) {
    let select = null;
    map.getInteractions().forEach(i => {
        if (i instanceof ol.interaction.Select) select = i;
    });
    if (!select) {
        select = new ol.interaction.Select();
        map.addInteraction(select);
    }
    return select;
}

export function findFeatureById(map, featureId) {
    if (!featureId) return null;
    let found = null;
    map.getLayers().forEach(layer => {
        if (layer instanceof ol.layer.Vector) {
            const source = layer.getSource();
            if (source && source.getFeatureById) {
                const f = source.getFeatureById(featureId);
                if (f) found = f;
            }
        }
    });
    return found;
}

// Attach for OpenLayers.Blazor compatibility (self-contained, idempotent)
if (typeof window !== 'undefined') {
    window.mapModule = window.mapModule || {};
    if (!window.mapModule.configureMap) {
        window.mapModule.configureMap = configureMap;
    }
}
