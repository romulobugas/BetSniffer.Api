document.addEventListener("DOMContentLoaded", () => {
    const linkContainer = document.getElementById("linkContainer");
    const addLinkButton = document.getElementById("addLink");
    const sendLinksButton = document.getElementById("sendLinks");
    const clearLinksButton = document.getElementById("clearLinks");

    // Adicionar uma nova caixa de texto para link
    function addLinkInput(initialValue = "") {
        const linkInput = document.createElement("input");
        linkInput.type = "text";
        linkInput.classList.add("link-input");
        linkInput.placeholder = "Insira o link para raspagem";
        linkInput.value = initialValue; // Define valor inicial, se houver

        // Adicionar evento para criar uma nova caixa ao pressionar "Enter"
        linkInput.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                addLinkInput(); // Adiciona uma nova caixa de texto
                linkInput.nextSibling.focus(); // Move o foco para a nova caixa
                event.preventDefault(); // Evita comportamento padrão
            }
        });

        linkContainer.appendChild(linkInput);
    }

    // Adicionar pelo menos uma caixa de texto ao carregar a página
    addLinkInput();

    // Evento para adicionar novas caixas de texto
    addLinkButton.addEventListener("click", () => addLinkInput());

    // Evento para limpar todas as caixas de texto e reiniciar com uma caixa vazia
    clearLinksButton.addEventListener("click", () => {
        linkContainer.innerHTML = ""; // Remove todas as caixas de texto
        addLinkInput(); // Adiciona uma caixa de texto vazia
    });

    // Evento para enviar os links
    sendLinksButton.addEventListener("click", () => {
        const linkInputs = document.querySelectorAll(".link-input");
        const useIa = document.getElementById("useIa")?.checked || false;
        const links = Array.from(linkInputs)
            .map(input => input.value.trim())
            .filter(link => link !== ""); // Filtra links vazios

        if (links.length === 0) {
            alert("Por favor, insira pelo menos um link.");
            return;
        }

        if (useIa) {
            // Enviar para o novo fluxo de IA
            const payload = links.map(link => ({ url: link }));
            
            fetch("/api/ia/enqueue", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            })
            .then(response => response.json())
            .then(data => {
                alert(data.message || "Links enviados para o Agente Visual.");
            })
            .catch(error => {
                console.error("Erro ao enviar para IA:", error);
                alert("Erro ao iniciar scraping visual.");
            });
            return;
        }

        // Fluxo tradicional
        const data = links.map(link => ({
            url: link,
            gameDate: new Date().toISOString(),
            homeTeam: 0,
            awayTeam: 0
        }));

        fetch("/api/BatchScraping/scrape", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(data),
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error("Erro ao enviar os links.");
                }
                return response.json();
            })
            .then(data => {
                alert(data.message || "Links enviados com sucesso.");
            })
            .catch(error => {
                console.error("Erro ao enviar os links:", error);
                alert("Ocorreu um erro ao enviar os links.");
            });
    });

});
