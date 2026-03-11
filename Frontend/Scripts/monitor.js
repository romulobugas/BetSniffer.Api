document.addEventListener('DOMContentLoaded', () => {
    let monitorInterval = null;

    // Monitora a troca de abas para iniciar/parar o refresh automático
    const tabButtons = document.querySelectorAll('.tab-button');
    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabName = btn.getAttribute('data-tab');
            if (tabName === 'monitor') {
                startMonitoring();
            } else {
                stopMonitoring();
            }
        });
    });

    // Se iniciar na aba monitor, já começa
    if (document.querySelector('.tab-button[data-tab="monitor"]').classList.contains('active')) {
        startMonitoring();
    }

    function startMonitoring() {
        fetchActivities();
        if (!monitorInterval) {
            monitorInterval = setInterval(fetchActivities, 3000); // Atualiza a cada 3 segundos
        }
    }

    function stopMonitoring() {
        if (monitorInterval) {
            clearInterval(monitorInterval);
            monitorInterval = null;
        }
    }

    async function fetchActivities() {
        try {
            const response = await fetch('/api/ia/activities');
            if (!response.ok) throw new Error('Falha ao buscar atividades');
            
            const activities = await response.json();
            renderActivities(activities);
            
            // Atualiza contador de ativos (Pendente ou Em Progresso)
            const activeCount = activities.filter(a => a.status === 'Pendente' || a.status === 'Em Progresso').length;
            document.getElementById('activeLinksCount').innerText = activeCount;
        } catch (error) {
            console.error('Erro no monitor:', error);
        }
    }

    function renderActivities(activities) {
        const container = document.getElementById('monitorContent');
        if (!activities || activities.length === 0) {
            container.innerHTML = '<div style="text-align:center; padding: 20px; color: #888;">Nenhuma atividade detectada recentemente.</div>';
            return;
        }

        let html = `
            <div class="monitor-table-container">
                <table class="monitor-table">
                    <thead>
                        <tr>
                            <th>Data/Hora</th>
                            <th>Site</th>
                            <th>Jogo (Identidade)</th>
                            <th>Status</th>
                            <th>Última Ação / Erro</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        activities.forEach(acc => {
            const statusClass = `status-${acc.status.toLowerCase().replace(' ', '-')}`;
            const timeStr = new Date(acc.timestamp).toLocaleTimeString();
            const teams = acc.homeTeam && acc.awayTeam ? `${acc.homeTeam} vs ${acc.awayTeam}` : 'Aguardando detecção...';
            const meta = (acc.league || acc.gameDate) ? `${acc.league || ''} | ${acc.gameDate || ''}` : acc.gameUrl;

            html += `
                <tr>
                    <td style="white-space:nowrap">${timeStr}</td>
                    <td><span class="site-badge">${acc.siteName}</span></td>
                    <td>
                        <div class="game-cell">
                            <span class="game-teams-label">${teams}</span>
                            <span class="game-meta-label">${meta}</span>
                            <a href="${acc.gameUrl}" target="_blank" class="url-link" title="${acc.gameUrl}">${acc.gameUrl}</a>
                        </div>
                    </td>
                    <td><span class="status-badge ${statusClass}">${acc.status}</span></td>
                    <td>
                        <div style="font-size: 13px;">
                            ${acc.lastAction || '-'}
                            ${acc.errorMessage ? `<div style="color:#ff6b6b; font-size: 11px; margin-top:5px;">${acc.errorMessage}</div>` : ''}
                        </div>
                    </td>
                </tr>
            `;
        });

        html += `
                    </tbody>
                </table>
            </div>
        `;

        container.innerHTML = html;
    }
});
