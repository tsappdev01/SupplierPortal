// Collapsible left navigation. Toggles a `data-sidebar` attribute on the document root
// ("collapsed" / "expanded") and persists the choice in localStorage. The initial value is
// applied by a tiny inline script in <head> (see App.razor) so there is no flash of the sidebar.
window.diSidebar = {
    KEY: 'di-sidebar',
    get: function () {
        return document.documentElement.getAttribute('data-sidebar') === 'collapsed';
    },
    set: function (collapsed) {
        document.documentElement.setAttribute('data-sidebar', collapsed ? 'collapsed' : 'expanded');
        try { localStorage.setItem(this.KEY, collapsed ? 'collapsed' : 'expanded'); } catch (e) { /* storage blocked */ }
    },
    toggle: function () {
        this.set(!this.get());
    }
};
