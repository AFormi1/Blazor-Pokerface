window.beforeCloseHandler = (function () {

    let sessionId = null;
    let playerId = null;
    function onBeforeUnload(e) {
        const data = JSON.stringify({
            sessionId: sessionId,
            playerId: playerId
        });

        const blob = new Blob([data], { type: "application/json" });

        navigator.sendBeacon("/api/session/exit", blob);
    }


    return {
        subscribe: function (sId, pId) {
            sessionId = sId;
            playerId = pId;

            window.addEventListener("beforeunload", onBeforeUnload);
        },

        unsubscribe: function () {
            window.removeEventListener("beforeunload", onBeforeUnload);
            sessionId = null;
            playerId = null;
        }
    };
})();
