function updateMonitor() {
    fetch('/api/BatchScraping/monitor')
        .then(response => response.json())
        .then(data => {
            const monitorContent = document.getElementById('monitorContent');
            const activeLinksCount = document.getElementById('activeLinksCount');
            activeLinksCount.textContent = data.activeTasksCount;

            monitorContent.innerHTML = ''; // Limpa os links anteriores
            data.activeTasks.forEach(link => {
                const linkElement = document.createElement('a');
                linkElement.href = link;
                linkElement.textContent = link;
                linkElement.target = '_blank'; // Abre em nova aba
                monitorContent.appendChild(linkElement);
            });
        })
        .catch(err => console.error('Erro ao atualizar o monitor:', err));
}

// Atualiza o monitor a cada 5 segundos
setInterval(updateMonitor, 5000);
updateMonitor();
