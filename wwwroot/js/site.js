(() => {
    const sidebar = document.getElementById("appSidebar");
    const sidebarToggle = document.getElementById("sidebarToggle");
    const sidebarBackdrop = document.getElementById("sidebarBackdrop");

    const closeSidebar = () => {
        sidebar?.classList.remove("open");
        sidebarBackdrop?.classList.remove("show");
        document.body.classList.remove("sidebar-open");
    };

    const openSidebar = () => {
        sidebar?.classList.add("open");
        sidebarBackdrop?.classList.add("show");
        document.body.classList.add("sidebar-open");
    };

    sidebarToggle?.addEventListener("click", () => {
        if (sidebar?.classList.contains("open")) {
            closeSidebar();
            return;
        }

        openSidebar();
    });

    sidebarBackdrop?.addEventListener("click", closeSidebar);

    window.addEventListener("resize", () => {
        if (window.innerWidth >= 992) {
            closeSidebar();
        }
    });

    window.setTimeout(() => {
        document.querySelectorAll(".app-alert").forEach((element) => {
            const alertInstance = bootstrap.Alert.getOrCreateInstance(element);
            alertInstance.close();
        });
    }, 5000);
})();
