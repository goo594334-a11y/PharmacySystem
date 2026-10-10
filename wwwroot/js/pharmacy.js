/**
 * Pharmacy Management System - Frontend Core Interactions
 */

document.addEventListener('DOMContentLoaded', function () {
    // 1. Mobile Sidebar Toggle & Backdrop
    const sidebar = document.getElementById('phSidebar');
    const toggleBtn = document.getElementById('phSidebarToggle');
    const closeBtn = document.getElementById('phSidebarClose');

    let backdrop = document.querySelector('.ph-sidebar-backdrop');
    if (!backdrop) {
        backdrop = document.createElement('div');
        backdrop.className = 'ph-sidebar-backdrop';
        document.body.appendChild(backdrop);
    }

    function openSidebar() {
        if (sidebar) sidebar.classList.add('show');
        if (backdrop) backdrop.classList.add('show');
    }

    function closeSidebar() {
        if (sidebar) sidebar.classList.remove('show');
        if (backdrop) backdrop.classList.remove('show');
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            if (sidebar && sidebar.classList.contains('show')) {
                closeSidebar();
            } else {
                openSidebar();
            }
        });
    }

    if (closeBtn) {
        closeBtn.addEventListener('click', closeSidebar);
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeSidebar);
    }

    // 2. Prevent Double Submissions on Forms
    const forms = document.querySelectorAll('form:not(.ph-allow-multiple-submits)');
    forms.forEach(function (form) {
        form.addEventListener('submit', function () {
            if (form.checkValidity && form.checkValidity()) {
                const submitBtn = form.querySelector('button[type="submit"]');
                if (submitBtn && !submitBtn.disabled) {
                    submitBtn.disabled = true;
                    const originalText = submitBtn.innerHTML;
                    submitBtn.setAttribute('data-original-text', originalText);
                    submitBtn.innerHTML = `
                        <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                        ${submitBtn.getAttribute('data-processing-text') || '...'}
                    `;
                }
            }
        });
    });

    // 3. Auto-hide alerts after 6 seconds
    const autoDismissAlerts = document.querySelectorAll('.ph-alert.alert-dismissible');
    autoDismissAlerts.forEach(function (alert) {
        setTimeout(function () {
            try {
                if (window.bootstrap && bootstrap.Alert) {
                    const bsAlert = new bootstrap.Alert(alert);
                    bsAlert.close();
                } else {
                    alert.style.transition = 'opacity 0.5s ease';
                    alert.style.opacity = '0';
                    setTimeout(() => alert.remove(), 500);
                }
            } catch (err) {
                // ignore
            }
        }, 6000);
    });

    // 4. Show/Hide Password Eye Button Toggle
    document.addEventListener('click', function (e) {
        const toggleBtn = e.target.closest('.toggle-password-btn');
        if (!toggleBtn) return;

        e.preventDefault();
        e.stopPropagation();

        const targetSelector = toggleBtn.getAttribute('data-target');
        const input = targetSelector
            ? document.querySelector(targetSelector)
            : toggleBtn.closest('.input-group')?.querySelector('input');
        if (!input) return;

        const eyeOpen = toggleBtn.querySelector('.eye-open');
        const eyeClosed = toggleBtn.querySelector('.eye-closed');

        if (input.type === 'password') {
            input.type = 'text';
            if (eyeOpen) eyeOpen.classList.add('d-none');
            if (eyeClosed) eyeClosed.classList.remove('d-none');
        } else {
            input.type = 'password';
            if (eyeOpen) eyeOpen.classList.remove('d-none');
            if (eyeClosed) eyeClosed.classList.add('d-none');
        }
    });

    // 5. Clear sensitive password fields on bfcache page restore
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            document.querySelectorAll('input[type="password"]').forEach(function (input) {
                input.value = '';
            });
        }
    });

    // 6. Dark / Light Theme Toggle
    const themeToggleBtn = document.getElementById('phThemeToggle');
    const sunIcon = document.getElementById('phThemeIconSun');
    const moonIcon = document.getElementById('phThemeIconMoon');

    function syncThemeIcons(theme) {
        if (!sunIcon || !moonIcon) return;
        if (theme === 'dark') {
            sunIcon.classList.remove('d-none');
            moonIcon.classList.add('d-none');
        } else {
            sunIcon.classList.add('d-none');
            moonIcon.classList.remove('d-none');
        }
    }

    const initialTheme = document.documentElement.getAttribute('data-theme') || 'light';
    syncThemeIcons(initialTheme);

    if (themeToggleBtn) {
        themeToggleBtn.addEventListener('click', function () {
            const current = document.documentElement.getAttribute('data-theme') || 'light';
            const next = current === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-theme', next);
            localStorage.setItem('ph_theme', next);
            syncThemeIcons(next);
        });
    }
});

