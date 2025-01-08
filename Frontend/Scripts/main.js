document.addEventListener("DOMContentLoaded", () => {
    fetch("/api/BatchScraping/version")
        .then(response => response.json())
        .then(data => {
            const versionElement = document.getElementById("version");
            versionElement.textContent = `v${data.version}`;
        })
        .catch(err => {
            console.error("Erro ao buscar a versão:", err);
        });
});
