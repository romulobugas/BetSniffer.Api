document.addEventListener("DOMContentLoaded", () => {
    const tabButtons = document.querySelectorAll(".tab-button");
    const tabPanes = document.querySelectorAll(".tab-pane");

    tabButtons.forEach(button => {
        button.addEventListener("click", () => {
            tabButtons.forEach(btn => btn.classList.remove("active"));
            tabPanes.forEach(pane => pane.classList.remove("active"));

            button.classList.add("active");
            const targetTab = button.getAttribute("data-tab");
            const targetPane = document.getElementById(targetTab);
            targetPane.classList.add("active");
        });
    });

    // Ativa a aba padrão ao carregar a página
    tabButtons[0].click();
});
