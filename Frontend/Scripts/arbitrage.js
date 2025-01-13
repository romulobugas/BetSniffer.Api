document.addEventListener("DOMContentLoaded", () => {
    const apiUrl = "/api/BatchScraping/arbitrage-results";
    const bankInput = document.getElementById("bankValue");
    const checkNewBetsButton = document.getElementById("checkNewBets");
    const arbitrageContainer = document.getElementById("arbitrageContent");

    // Função para calcular o valor das apostas proporcionalmente às odds
    const calculateStakes = (bankValue, odd1, odd2) => {
        const stake1 = (bankValue * odd2) / (odd1 + odd2); // Calcula para a primeira odd
        const stake2 = (bankValue * odd1) / (odd1 + odd2); // Calcula para a segunda odd
        return [stake1.toFixed(2), stake2.toFixed(2)];
    };

    // Recalcula os valores da outra aposta e ajusta a banca
    const recalculateBetValues = (editedValue, otherBetElement, multiplierEdited, multiplierOther) => {
        const totalBank = editedValue * (multiplierEdited + multiplierOther) / multiplierOther;

        // Recalcula o valor proporcional da outra casa
        const otherStake = totalBank - editedValue;

        // Atualiza a interface com os novos valores
        otherBetElement.textContent = `${otherStake.toFixed(2)}`;
        bankInput.value = totalBank.toFixed(2);
    };

    // Adiciona evento ao campo editável
    const activateEditableFields = () => {
        document.querySelectorAll(".editable-bet-value").forEach((editable) => {
            editable.addEventListener("dblclick", (e) => {
                const currentElement = e.target;
                currentElement.contentEditable = "true";
                currentElement.focus();
            });

            editable.addEventListener("blur", (e) => {
                const currentElement = e.target;
                currentElement.contentEditable = "false";

                const newValue = parseFloat(currentElement.textContent.replace("R$", "").trim());
                if (!isNaN(newValue) && newValue > 0) {
                    const multiplierEdited = parseFloat(currentElement.dataset.multiplier);
                    const otherBetElement = document.querySelector(
                        `.editable-bet-value:not([data-multiplier="${multiplierEdited}"])`
                    );
                    const multiplierOther = parseFloat(otherBetElement.dataset.multiplier);

                    recalculateBetValues(newValue, otherBetElement, multiplierEdited, multiplierOther);
                } else {
                    currentElement.textContent = `R$${currentElement.dataset.initialValue}`; // Restaura valor inicial se inválido
                }
            });

            editable.addEventListener("keydown", (e) => {
                if (e.key === "Enter") {
                    e.preventDefault();
                    e.target.blur(); // Finaliza edição ao pressionar Enter
                }
            });
        });
    };

    // Função para criar o mosaico (alterado)
    const createMosaic = (arbitrage, bankValue = 0) => {
        const mosaic = document.createElement("div");
        mosaic.classList.add("mosaic");

        // Calcula os valores das apostas com base nas odds e na banca
        const [stakeX, stakeY] = calculateStakes(bankValue, arbitrage.multiplierX, arbitrage.multiplierY);

        // Obtém os nomes dos times separados por " ; "
        const homeTeams = arbitrage.homeTeam.split(";");
        const awayTeams = arbitrage.awayTeam.split(";");
        let currentHomeTeamIndex = 0;
        let currentAwayTeamIndex = 0;

        const mosaicContent = `
            <div class="mosaic-header">
                <h3>Lucro: ${arbitrage.arbitrageLucroPercent.toFixed(2)}%</h3>
            </div>
            <div class="mosaic-body">
                <div class="mosaic-column">
                    <img src="../Assets/images/${arbitrage.siteNameX.toLowerCase()}.png" alt="${arbitrage.siteNameX}" class="site-icon" />
                    <p class="site-name">${arbitrage.siteNameX}</p>
                    <p class="market-info">${arbitrage.tagNameX || "Mercado não especificado"}</p>
                    <p class="bet-info">${arbitrage.overUnderX || "N/A"}: ${arbitrage.betAmountX.toFixed(2)}</p>
                    <p class="bet-info">ODD: ${arbitrage.multiplierX.toFixed(2)}</p>
                    <p class="bet-info">
                        Valor da Aposta R$:
                        <span class="editable-bet-value"
                              contenteditable="false"
                              data-initial-value="${stakeX}" 
                              data-multiplier="${arbitrage.multiplierX}">
                              ${stakeX}
                        </span>
                    </p>
                </div>
                <div class="team-container">
                    <div class="team-buttons">
                        <button class="team-button home-team" title="Casa">${homeTeams[currentHomeTeamIndex]}</button>
                        <span class="vs-text">VS</span>
                        <button class="team-button away-team" title="Visitante">${awayTeams[currentAwayTeamIndex]}</button>
                    </div>              
                    <p class="game-date">${arbitrage.gameDateX ? new Date(arbitrage.gameDateX).toLocaleString("pt-BR") : "Data não disponível"}</p>
                </div>                
                <div class="mosaic-column">
                    <img src="../Assets/images/${arbitrage.siteNameY.toLowerCase()}.png" alt="${arbitrage.siteNameY}" class="site-icon" />
                    <p class="site-name">${arbitrage.siteNameY}</p>
                    <p class="market-info">${arbitrage.tagNameY || "Mercado não especificado"}</p>
                    <p class="bet-info">${arbitrage.overUnderY || "N/A"}: ${arbitrage.betAmountY.toFixed(2)}</p>
                    <p class="bet-info">ODD: ${arbitrage.multiplierY.toFixed(2)}</p>
                    <p class="bet-info">
                        Valor da Aposta R$:
                        <span class="editable-bet-value"
                              contenteditable="false"
                              data-initial-value="${stakeY}" 
                              data-multiplier="${arbitrage.multiplierY}">
                              ${stakeY}
                        </span>
                    </p>

                </div>
            </div>
        `;


        mosaic.innerHTML = mosaicContent;
        arbitrageContainer.appendChild(mosaic);
        activateEditableFields(); // Ativa eventos após inserir no DOM

        // Alternar nomes dos times ao clicar
        const homeTeamElement = mosaic.querySelector(".home-team");
        const awayTeamElement = mosaic.querySelector(".away-team");

        homeTeamElement.addEventListener("click", () => {
            currentHomeTeamIndex = (currentHomeTeamIndex + 1) % homeTeams.length;
            homeTeamElement.textContent = homeTeams[currentHomeTeamIndex];
        });

        awayTeamElement.addEventListener("click", () => {
            currentAwayTeamIndex = (currentAwayTeamIndex + 1) % awayTeams.length;
            awayTeamElement.textContent = awayTeams[currentAwayTeamIndex];
        });
    };




    // Função para atualizar os valores ao alterar a banca
    const updateStakeValues = () => {
        const bankValue = parseFloat(bankInput.value) || 0;
        fetchArbitrageData(bankValue);
    };

    // Função para buscar dados e criar os mosaicos
    const fetchArbitrageData = async (bankValue = 0) => {
        try {
            const response = await fetch(apiUrl);
            if (!response.ok) {
                throw new Error("Erro ao buscar dados de arbitragem");
            }

            const data = await response.json();
            arbitrageContainer.innerHTML = "";

            data.forEach((arbitrage) => createMosaic(arbitrage, bankValue));
        } catch (error) {
            console.error("Erro ao buscar dados:", error);
            arbitrageContainer.innerHTML = "<p>Erro ao carregar os dados.</p>";
        }
    };

    // Evento para atualizar os valores conforme o usuário altera o valor da banca
    bankInput.addEventListener("input", updateStakeValues);

    // Evento para o botão "Verificar Novas Apostas"
    checkNewBetsButton.addEventListener("click", async () => {
        try {
            await fetch("/api/BatchScraping/execute-arbitrage", { method: "POST" });
            updateStakeValues(); // Atualiza os valores após verificar novas apostas
        } catch (error) {
            console.error("Erro ao verificar novas apostas:", error);
        }
    });

    // Inicializa os dados na página com banca zero
    fetchArbitrageData(0);
});
