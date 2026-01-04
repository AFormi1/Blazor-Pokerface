window.getElementSize = (element) => {
    if (!element) return { width: 0, height: 0, top: 0, left: 0 };
    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height, top: rect.top, left: rect.left };
};

window.registerResizeHandler = (dotNetRef) => {
    if (window.__gameTableResizeRegistered)
        return;

    window.__gameTableResizeRegistered = true;

    let resizeTimer;

    window.addEventListener("resize", () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            dotNetRef.invokeMethodAsync("OnWindowResize");
        }, 50);
    });
};


window.registerSvgLoadCallback = (objectElement, dotNetRef) => {
    if (!objectElement) return;

    const onLoaded = () => {
        dotNetRef.invokeMethodAsync("OnSvgLoaded");
    };

    // Already loaded (cache case)
    if (objectElement.contentDocument) {
        onLoaded();
        return;
    }

    objectElement.addEventListener("load", onLoaded, { once: true });
};



window.getRenderedImageRect = (element) => {
    if (!element) return { x: 0, y: 0, width: 0, height: 0 };

    const r = element.getBoundingClientRect();
    return {
        x: r.left,
        y: r.top,
        width: r.width,
        height: r.height
    };
};


window.getSvgElementRects = (objectElement, ids) => {
    if (!objectElement?.contentDocument) return {};

    const svgDoc = objectElement.contentDocument;
    const objectRect = objectElement.getBoundingClientRect();
    const result = {};

    for (const id of ids) {
        const el = svgDoc.getElementById(id);
        if (!el) continue;

        const bbox = el.getBBox();
        const matrix = el.getScreenCTM();
        if (!matrix) continue;

        // Transform SVG bbox corners
        const p1 = new DOMPoint(bbox.x, bbox.y).matrixTransform(matrix);
        const p2 = new DOMPoint(
            bbox.x + bbox.width,
            bbox.y + bbox.height
        ).matrixTransform(matrix);

        result[id] = {
            x: objectRect.left + p1.x,
            y: objectRect.top + p1.y,
            width: Math.abs(p2.x - p1.x),
            height: Math.abs(p2.y - p1.y)
        };
    }

    return result;
};








