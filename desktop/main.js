const { app, BrowserWindow, Menu } = require('electron');

app.whenReady().then(() => {
  Menu.setApplicationMenu(null);
  const win = new BrowserWindow({
    width: 1280, height: 720,
    backgroundColor: '#04050a',
    autoHideMenuBar: true,
    title: 'VampFrost',
    webPreferences: { contextIsolation: true }
  });
  win.setMenuBarVisibility(false);
  win.maximize();
  win.loadFile('VampFrost.html');
  // F11 fullscreen toggle, Alt+F4 quits as usual
  win.webContents.on('before-input-event', (e, input) => {
    if (input.key === 'F11' && input.type === 'keyDown') {
      win.setFullScreen(!win.isFullScreen());
      e.preventDefault();
    }
  });
});
app.on('window-all-closed', () => app.quit());
