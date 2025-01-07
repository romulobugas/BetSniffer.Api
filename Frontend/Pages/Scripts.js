document.addEventListener("DOMContentLoaded", () => {
    // Alternar abas
    const tabButtons = document.querySelectorAll(".tab-button");
    const tabPanes = document.querySelectorAll(".tab-pane");

    tabButtons.forEach(button => {
        button.addEventListener("click", () => {
            tabButtons.forEach(btn => btn.classList.remove("active"));
            tabPanes.forEach(pane => pane.classList.remove("active"));

            button.classList.add("active");
            document.getElementById(button.getAttribute("data-tab")).classList.add("active");
        });
    });

    // Carregar lista de sites com checkboxes
    const siteCheckboxList = document.getElementById("siteCheckboxList");
    fetch("/api/BatchScraping/sites")
        .then(response => {
            if (!response.ok) {
                throw new Error(`Erro na API: ${response.status} - ${response.statusText}`);
            }
            return response.json();
        })
        .then(sites => {
            // Verifica se há sites retornados
            if (!sites || sites.length === 0) {
                siteCheckboxList.innerHTML = "<p>Nenhum site disponível.</p>";
                return;
            }

            // Limpa o conteúdo existente
            siteCheckboxList.innerHTML = "";

            // Preenche os checkboxes com nomes dos sites
            sites.forEach(site => {
                const checkbox = document.createElement("input");
                checkbox.type = "checkbox";
                checkbox.id = `site-${site.siteId}`;
                checkbox.value = site.siteId;
                checkbox.checked = true;

                const label = document.createElement("label");
                label.htmlFor = `site-${site.siteId}`;
                label.textContent = site.name;

                const div = document.createElement("div");
                div.classList.add("site-checkbox-item");
                div.style.display = "inline-block"; // Coloca em linha
                div.style.margin = "5px"; // Adiciona espaçamento
                div.appendChild(checkbox);
                div.appendChild(label);

                siteCheckboxList.appendChild(div);
            });
        })
        .catch(err => {
            console.error("Erro ao carregar a lista de sites:", err);
            alert("Erro ao carregar a lista de sites. Verifique o console para mais detalhes.");
        });

    // Adicionar funcionalidade para marcar/desmarcar todos os sites
    const toggleAllButton = document.createElement("button");
    toggleAllButton.textContent = "Marcar/Desmarcar Todos";
    toggleAllButton.id = "toggleAll";
    toggleAllButton.style.marginTop = "10px";
    siteCheckboxList.parentNode.appendChild(toggleAllButton);

    toggleAllButton.addEventListener("click", () => {
        const checkboxes = document.querySelectorAll("#siteCheckboxList input[type=checkbox]");
        const allChecked = Array.from(checkboxes).every(checkbox => checkbox.checked);
        checkboxes.forEach(checkbox => checkbox.checked = !allChecked);
    });

    // Enviar solicitação
    document.getElementById("sendRequest").addEventListener("click", () => {
        const selectedSiteIds = Array.from(
            document.querySelectorAll("#siteCheckboxList input[type=checkbox]:checked")
        ).map(checkbox => parseInt(checkbox.value));

        const startDate = document.getElementById("startDate").value;
        const endDate = document.getElementById("endDate").value;

        fetch(`/api/BatchScraping/batch-update-same-games?startDate=${startDate}&endDate=${endDate}&siteIds=${selectedSiteIds.join(",")}`, {
            method: "PUT"
        })
            .then(response => response.json())
            .then(data => {
                console.log("Resposta da API:", data);
                alert(data.message);
            })
            .catch(err => {
                console.error("Erro:", err);
                alert("Erro ao enviar solicitação.");
            });
    });
});
