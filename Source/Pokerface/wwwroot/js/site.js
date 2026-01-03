window.getElementSize = (element) => {
    if (!element) return { width: 0, height: 0, top: 0, left: 0 };
    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height, top: rect.top, left: rect.left };
};

window.registerResizeHandler = (dotnetHelper) => {
    window.addEventListener('resize', () => {
        dotnetHelper.invokeMethodAsync('OnWindowResize');
    });
};

window.getRenderedImageRect = (img) => {
    if (!img || !img.complete || img.naturalWidth === 0) {
        return {
            width: 0,
            height: 0,
            left: 0,
            top: 0
        };
    }

    const container = img.getBoundingClientRect();

    const naturalWidth = img.naturalWidth;
    const naturalHeight = img.naturalHeight;

    const containerRatio = container.width / container.height;
    const imageRatio = naturalWidth / naturalHeight;

    let width, height, offsetX, offsetY;

    if (containerRatio > imageRatio) {
        height = container.height;
        width = height * imageRatio;
        offsetX = (container.width - width) / 2;
        offsetY = 0;
    } else {
        width = container.width;
        height = width / imageRatio;
        offsetX = 0;
        offsetY = (container.height - height) / 2;
    }

    return {
        width,
        height,
        left: container.left + offsetX,
        top: container.top + offsetY
    };
};





