// Fonctions du navigateur utilisées par le client web (module chargé à la demande par BrowserScripts).

// Ouvre le sélecteur de fichier ; rend { fileName, content }, ou null si l'utilisateur annule.
export function openFile() {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json,application/json';
        input.addEventListener('change', async () => {
            const file = input.files && input.files[0];
            resolve(file ? { fileName: file.name, content: await file.text() } : null);
        });
        input.addEventListener('cancel', () => resolve(null));
        input.click();
    });
}

// Enregistre en téléchargeant le fichier sous le nom proposé.
export function downloadFile(fileName, content) {
    const url = URL.createObjectURL(new Blob([content], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// Mode sombre demandé par le système (premier lancement, sans thème mémorisé).
export function prefersDark() {
    return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
}

// Retire la barre de chargement de la page, une fois l'interface affichée.
export function hideBoot() {
    const boot = document.getElementById('wh-boot');
    if (boot) {
        boot.remove();
    }
}
