document.addEventListener("DOMContentLoaded", () => {
    const siteCheckboxList = document.getElementById("siteCheckboxList");

    // Carregar lista de sites
    fetch("/api/BatchScraping/sites")
        .then(response => response.json())
        .then(sites => {
            siteCheckboxList.innerHTML = "";
            sites.forEach(site => {
                const siteButton = document.createElement("div");
                siteButton.classList.add("site-button", "selected"); // Já começa selecionado
                siteButton.dataset.siteId = site.siteId;

                const siteIcon = document.createElement("img");
                siteIcon.src = `../Assets/images/${site.name.toLowerCase()}.png`;
                siteIcon.alt = site.name;
                siteIcon.classList.add("site-icon");

                const siteName = document.createElement("span");
                siteName.textContent = site.name;
                siteName.classList.add("site-name");

                // Adicionar evento de clique para selecionar/deselecionar
                siteButton.addEventListener("click", () => {
                    siteButton.classList.toggle("selected");
                });

                siteButton.appendChild(siteIcon);
                siteButton.appendChild(siteName);
                siteCheckboxList.appendChild(siteButton);
            });
        })
        .catch(err => {
            console.error("Erro ao carregar sites:", err);
        });

    // Marcar/Desmarcar todos os sites
    document.getElementById("toggleAll").addEventListener("click", () => {
        const allButtons = siteCheckboxList.querySelectorAll(".site-button");
        const allSelected = Array.from(allButtons).every(btn => btn.classList.contains("selected"));
        allButtons.forEach(btn => {
            if (allSelected) {
                btn.classList.remove("selected");
            } else {
                btn.classList.add("selected");
            }
        });
    });

    // Enviar solicitação
    document.getElementById("sendRequest").addEventListener("click", () => {
        const selectedSites = Array.from(siteCheckboxList.querySelectorAll(".site-button.selected"))
            .map(btn => parseInt(btn.dataset.siteId));
        const startDate = document.getElementById("startDate").value;
        const endDate = document.getElementById("endDate").value;

        // Validação: deve selecionar pelo menos 2 casas
        if (selectedSites.length < 2) {
            alert("Você deve selecionar pelo menos 2 casas para iniciar o scraping.");
            return;
        }

        fetch(`/api/BatchScraping/batch-update-same-games?startDate=${startDate}&endDate=${endDate}&siteIds=${selectedSites.join(",")}`, {
            method: "PUT",
        })
            .then(response => response.json())
            .then(data => {
                alert(data.message);
            })
            .catch(err => {
                console.error("Erro ao enviar solicitação:", err);
                alert("Erro ao enviar solicitação.");
            });
    });
});
