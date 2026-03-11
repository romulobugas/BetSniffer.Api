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
        const selectedSiteButtons = Array.from(siteCheckboxList.querySelectorAll(".site-button.selected"));
        const selectedSiteIds = selectedSiteButtons.map(btn => parseInt(btn.dataset.siteId));
        const selectedSiteNames = selectedSiteButtons.map(btn => btn.querySelector(".site-name").textContent);
        
        const startDate = document.getElementById("startDate").value;
        const endDate = document.getElementById("endDate").value;
        const useIa = document.getElementById("useIaUpdate")?.checked || false;

        // Validação: deve selecionar pelo menos 1 casa
        if (selectedSiteIds.length === 0) {
            alert("Você deve selecionar pelo menos 1 casa para iniciar a raspagem.");
            return;
        }

        // Validação: deve selecionar as datas
        if (startDate == "" || endDate == "") {
            alert("Você deve escolher as datas para iniciar a raspagem.");
            return;
        }

        if (useIa) {
            // Fluxo de IA para atualização em lote
            const payload = {
                siteNames: selectedSiteNames,
                startDate: startDate,
                endDate: endDate
            };

            fetch("/api/ia/batch-enqueue", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            })
            .then(response => response.json())
            .then(data => {
                alert(data.message || "Solicitação de atualização via IA enviada com sucesso.");
            })
            .catch(err => {
                console.error("Erro ao enviar solicitação de IA:", err);
                alert("Erro ao iniciar atualização visual via IA.");
            });
            return;
        }

        // Fluxo tradicional
        fetch(`/api/BatchScraping/batch-update-same-games?startDate=${startDate}&endDate=${endDate}&siteIds=${selectedSiteIds.join(",")}`, {
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
