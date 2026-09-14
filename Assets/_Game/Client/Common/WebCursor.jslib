mergeInto(LibraryManager.library, {
  Game_CanRequestPointerLock: function () {
    return document.pointerLockElement === Module.canvas ||
      !!(navigator.userActivation && navigator.userActivation.isActive);
  }
});
