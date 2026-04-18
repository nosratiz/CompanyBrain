// Auth token storage functions for Blazor interop
window.authStorage = {
    setToken: function (key, value) {
        localStorage.setItem(key, value);
    },
    getToken: function (key) {
        return localStorage.getItem(key);
    },
    removeToken: function (key) {
        localStorage.removeItem(key);
    },
    setSession: function (data) {
        localStorage.setItem('auth_session', JSON.stringify(data));
    },
    getSession: function () {
        const data = localStorage.getItem('auth_session');
        return data ? JSON.parse(data) : null;
    },
    clearSession: function () {
        localStorage.removeItem('auth_session');
    }
};
