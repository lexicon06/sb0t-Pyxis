window.sb0tLang = {
    get: function () {
        try {
            var v = localStorage.getItem('sb0t.lang');
            return v === 'es' ? 'es' : 'en';
        } catch (e) {
            return 'en';
        }
    },
    set: function (lang) {
        try {
            localStorage.setItem('sb0t.lang', lang === 'es' ? 'es' : 'en');
        } catch (e) { }
    }
};
