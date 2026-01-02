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

