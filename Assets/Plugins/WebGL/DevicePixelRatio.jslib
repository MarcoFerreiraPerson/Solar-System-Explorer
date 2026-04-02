mergeInto(LibraryManager.library, {
    GetDevicePixelRatio: function () {
        return window.devicePixelRatio || 1;
    }
});
