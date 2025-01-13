document.addEventListener("DOMContentLoaded", () => {
    const apiUrl = "/api/BatchScraping/arbitrage-results";
    const bankInput = document.getElementById("bankValue");
    const checkNewBetsButton = document.getElementById("checkNewBets");
    const arbitrageContainer = document.getElementById("arbitrageContent");

    // Função para calcular os valores de aposta proporcionalmente
    const calculateStakes = (bankValue, odd1, odd2) => {
        const stake1 = (bankValue * odd2) / (odd1 + odd2);
        const stake2 = (bankValue * odd1) / (odd1 + odd2);
        return [stake1.toFixed(2), stake2.toFixed(2)];
    };

    // Recalcula os valores de todas as linhas com base no valor total da banca
    const recalculateAllStakes = (bankValue) => {
        document.querySelectorAll(".arbitrage-row").forEach((row) => {
            const stakeInputs = row.querySelectorAll(".stake-input");
            const oddElements = row.querySelectorAll(".odd");
            const odd1 = parseFloat(oddElements[0].textContent.replace("ODD: ", ""));
            const odd2 = parseFloat(oddElements[1].textContent.replace("ODD: ", ""));

            const [stake1, stake2] = calculateStakes(bankValue, odd1, odd2);

            // Atualiza os campos de valor
            stakeInputs[0].value = stake1;
            stakeInputs[1].value = stake2;
        });
    };

    // Adiciona máscara para aceitar apenas valores numéricos
    const applyNumericMask = (input) => {
        input.addEventListener("input", (e) => {
            const currentValue = e.target.value;
            const sanitizedValue = currentValue.replace(/[^0-9.]/g, ""); // Remove caracteres inválidos

            // Apenas atualiza o campo se o valor mudou
            if (currentValue !== sanitizedValue) {
                e.target.value = sanitizedValue;
            }
        });
    };




    // Atualiza os valores e recalcula as apostas
    const handleStakeInputChange = (input) => {
        const row = input.closest(".arbitrage-row");
        const oddElements = row.querySelectorAll(".odd");
        const odd1 = parseFloat(oddElements[0].textContent.replace("ODD: ", ""));
        const odd2 = parseFloat(oddElements[1].textContent.replace("ODD: ", ""));
        const otherInput = [...row.querySelectorAll(".stake-input")].find((el) => el !== input);

        const newValue = parseFloat(input.value);
        if (!isNaN(newValue) && newValue > 0) {
            const totalBank = (newValue * (odd1 + odd2)) / odd2;
            const otherStake = totalBank - newValue;

            // Atualiza o valor no outro campo
            otherInput.value = otherStake.toFixed(2);

            // Atualiza o valor da banca
            bankInput.value = totalBank.toFixed(2);
        }
    };




    // Ativa os campos de input para as apostas
    const activateStakeInputs = () => {
        document.querySelectorAll(".stake-input").forEach((stakeInput) => {
            applyNumericMask(stakeInput);

            stakeInput.addEventListener("input", (e) => {
                handleStakeInputChange(e.target);
            });

            stakeInput.addEventListener("focus", (e) => {
                e.target.select(); // Seleciona todo o texto ao focar
            });
        });

        // Aplica a máscara ao campo "Valor da Banca"
        applyNumericMask(bankInput);

        bankInput.addEventListener("input", updateStakeValues);
    };




    // Atualiza os valores ao alterar a banca
    const updateStakeValues = () => {
        const bankValue = parseFloat(bankInput.value) || 0;
        recalculateAllStakes(bankValue);
    };

    // Evento para atualizar os valores conforme o usuário altera o valor da banca
    bankInput.addEventListener("input", updateStakeValues);

    // Evento para o botão "Verificar Novas Apostas"
    checkNewBetsButton.addEventListener("click", async () => {
        try {
            const response = await fetch("/api/BatchScraping/execute-arbitrage", { method: "POST" });
            const data = await response.json();

            if (data.message) {
                showTemporaryMessage(data.message); // Exibe a mensagem rápida no topo
            }

            fetchArbitrageData(); // Atualiza os dados após verificar novas apostas
        } catch (error) {
            console.error("Erro ao verificar novas apostas:", error);
            showTemporaryMessage("Erro ao verificar novas apostas. Tente novamente.");
        }
    });

    const showTemporaryMessage = (message) => {
        // Cria a mensagem temporária
        const messageBox = document.createElement("div");
        messageBox.textContent = message;
        messageBox.classList.add("temporary-message");

        // Estilo fixo no canto superior direito
        messageBox.style.position = "fixed";
        messageBox.style.top = "10px";
        messageBox.style.right = "10px";
        messageBox.style.backgroundColor = "#f0ad4e";
        messageBox.style.color = "#fff";
        messageBox.style.padding = "10px 20px";
        messageBox.style.borderRadius = "5px";
        messageBox.style.boxShadow = "0 2px 5px rgba(0,0,0,0.3)";
        messageBox.style.zIndex = "9999";
        messageBox.style.fontSize = "14px";

        document.body.appendChild(messageBox);

        // Remove a mensagem após 3 segundos
        setTimeout(() => {
            if (messageBox.parentNode) {
                messageBox.parentNode.removeChild(messageBox);
            }
        }, 3000);
    };



    const createArbitrageRow = (arbitrage) => {
        const row = document.createElement("div");
        row.classList.add("arbitrage-row");

        row.innerHTML = `
        <div class="arbitrage-content">
            <!-- Coluna Lucro -->
            <div class="profit">
                <span>${arbitrage.arbitrageLucroPercent.toFixed(2)}%</span>
            </div>

            <!-- Coluna Casa X -->
            <div class="arbitrage-column">
                <div class="arbitrage-grid">
                    <img src="../Assets/icons/${arbitrage.siteNameX.toLowerCase()}.ico" alt="${arbitrage.siteNameX}" class="site-icon-odd" />
                    <span class="site-name-odd">${arbitrage.siteNameX}</span>
                    <span class="teams">${arbitrage.gameX}</span>
                </div>
                <div class="arbitrage-grid">
                    <img src="../Assets/icons/${arbitrage.siteNameY.toLowerCase()}.ico" alt="${arbitrage.siteNameY}" class="site-icon-odd" />
                    <span class="site-name-odd">${arbitrage.siteNameY}</span>
                   <span class="teams">${arbitrage.gameY}</span>
                </div>
            </div>

            <!-- Coluna da Data -->
            <div class="arbitrage-date">
                <div>${new Date(arbitrage.gameDateX).toLocaleDateString("pt-BR")}</div>
                <div>${new Date(arbitrage.gameDateX).toLocaleTimeString("pt-BR")}</div>
            </div>

            <!-- Coluna de Mercados, Odds e Valores -->
            <div class="arbitrage-column">
                <div class="arbitrage-grid">
                    <span class="market">${arbitrage.tagNameX || "Mercado não especificado"}</span>
                </div>
                <div class="arbitrage-grid">
                    <span class="market">${arbitrage.tagNameY || "Mercado não especificado"}</span>
                </div>
            </div>
            <div class="arbitrage-column">
                <div class="arbitrage-grid">
                    <span class="market">${arbitrage.overUnderX || "N/A"}: ${arbitrage.betAmountX || "N/D"}</span>
                    <span class="odd">ODD: ${arbitrage.multiplierX.toFixed(2)}</span>
                    <span class="market">Valor da Aposta:</span>
                    <input class="stake-input input-field" type="text" data-multiplier="${arbitrage.multiplierX}" value="0.00" />
                </div>
                <div class="arbitrage-grid">
                    <span class="market">${arbitrage.overUnderY || "N/A"}: ${arbitrage.betAmountY || "N/D"}</span>
                    <span class="odd">ODD: ${arbitrage.multiplierY.toFixed(2)}</span>
                    <span class="market">Valor da Aposta:</span>
                    <input class="stake-input input-field" type="text" data-multiplier="${arbitrage.multiplierY}" value="0.00" />
                </div>
            </div>
        </div>
    `;

        arbitrageContainer.appendChild(row);
    };

    const fetchArbitrageData = async (bankValue = 0) => {
        try {
            const response = await fetch("/api/BatchScraping/arbitrage-results");

            if (response.status === 404) {
                const errorData = await response.json();
                showArbitrageMessage(errorData.message || "Nenhum resultado de arbitragem encontrado.");
                return;
            }

            if (!response.ok) throw new Error("Erro ao buscar dados de arbitragem");

            const data = await response.json();

            arbitrageContainer.innerHTML = ""; // Limpa o container antes de adicionar novos dados

            if (data.length === 0) {
                showArbitrageMessage("Nenhum resultado de arbitragem encontrado.");
                return;
            }

            // Remove mensagens antigas e exibe os resultados
            const existingMessage = document.querySelector(".arbitrage-message");
            if (existingMessage) existingMessage.remove();

            data.forEach((arbitrage) => createArbitrageRow(arbitrage));
            activateStakeInputs(); // Ativa os eventos de edição após carregar os dados
        } catch (error) {
            console.error("Erro ao carregar dados:", error);
            showArbitrageMessage("Erro ao carregar os dados. Tente novamente.");
        }
    };


    const removeArbitrageMessage = () => {
        const existingMessage = document.querySelector(".arbitrage-message");
        if (existingMessage) existingMessage.remove();
    };

    const showArbitrageMessage = (message) => {
        // Remove mensagens existentes
        const existingMessage = document.querySelector(".arbitrage-message");
        if (existingMessage) existingMessage.remove();

        // Cria o elemento de mensagem
        const messageBox = document.createElement("div");
        messageBox.textContent = message;
        messageBox.classList.add("arbitrage-message");

        // Estilo fixo para centralização apenas na aba de arbitragem
        messageBox.style.position = "relative";
        messageBox.style.margin = "50px auto"; // Ajusta o espaçamento para centralizar
        messageBox.style.padding = "10px 20px";
        messageBox.style.borderRadius = "5px";
        messageBox.style.textAlign = "center";
        messageBox.style.backgroundColor = "#ffcc00";
        messageBox.style.color = "#000";
        messageBox.style.fontSize = "16px";
        messageBox.style.fontWeight = "bold";
        messageBox.style.width = "fit-content";

        // Adiciona a mensagem ao container de arbitragem
        arbitrageContainer.innerHTML = ""; // Garante que o container está vazio antes de adicionar a mensagem
        arbitrageContainer.appendChild(messageBox);
    };


    const showCenterMessage = (message) => {
        // Limpa mensagens existentes
        const existingMessage = document.querySelector(".center-message");
        if (existingMessage) existingMessage.remove();

        // Cria o elemento para exibir a mensagem
        const messageBox = document.createElement("div");
        messageBox.textContent = message;
        messageBox.classList.add("center-message");

        // Estilo básico para centralização da mensagem
        messageBox.style.position = "absolute";
        messageBox.style.top = "50%";
        messageBox.style.left = "50%";
        messageBox.style.transform = "translate(-50%, -50%)";
        messageBox.style.backgroundColor = "#ffcc00";
        messageBox.style.color = "#000";
        messageBox.style.padding = "10px 20px";
        messageBox.style.borderRadius = "5px";
        messageBox.style.boxShadow = "0 4px 6px rgba(0, 0, 0, 0.1)";
        messageBox.style.fontSize = "16px";
        messageBox.style.fontWeight = "bold";
        messageBox.style.textAlign = "center";
        messageBox.style.zIndex = "1000";

        // Adiciona a mensagem ao body
        document.body.appendChild(messageBox);

        // Remove a mensagem automaticamente após 5 segundos
        setTimeout(() => {
            if (messageBox.parentNode) {
                messageBox.parentNode.removeChild(messageBox);
            }
        }, 5000);
    };



    // Inicializar a tabela ao carregar a página
    fetchArbitrageData();
});
