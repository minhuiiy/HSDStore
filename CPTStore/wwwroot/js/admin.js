// JavaScript for admin area

document.addEventListener('DOMContentLoaded', function() {
    // Toggle sidebar
    const sidebarToggle = document.getElementById('sidebarToggle');
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', function(e) {
            e.preventDefault();
            document.body.classList.toggle('sb-sidenav-toggled');
            localStorage.setItem('sb|sidebar-toggle', document.body.classList.contains('sb-sidenav-toggled'));
        });
    }
    
    // Toggle settings submenu
    const settingsLink = document.querySelector('a[asp-controller="Settings"][asp-action="Index"]');
    if (settingsLink) {
        settingsLink.addEventListener('click', function(event) {
            // Only toggle if clicking directly on the main settings link
            if (event.target === settingsLink || settingsLink.contains(event.target)) {
                event.preventDefault();
                const submenu = settingsLink.nextElementSibling;
                if (submenu && submenu.classList.contains('ms-4')) {
                    submenu.classList.toggle('d-none');
                    submenu.classList.toggle('d-block');
                }
            }
        });
    }

    // Check for saved sidebar state
    if (localStorage.getItem('sb|sidebar-toggle') === 'true') {
        document.body.classList.add('sb-sidenav-toggled');
    }

    // Initialize tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Initialize popovers
    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    var popoverList = popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });

    // Confirm delete
    document.querySelectorAll('.confirm-delete').forEach(function(button) {
        button.addEventListener('click', function(e) {
            if (!confirm('Bạn có chắc chắn muốn xóa mục này không?')) {
                e.preventDefault();
            }
        });
    });

    // Image preview
    const imageInput = document.getElementById('imageInput');
    const imagePreview = document.getElementById('imagePreview');
    
    if (imageInput && imagePreview) {
        imageInput.addEventListener('change', function() {
            if (this.files && this.files[0]) {
                const reader = new FileReader();
                
                reader.onload = function(e) {
                    imagePreview.src = e.target.result;
                    imagePreview.style.display = 'block';
                }
                
                reader.readAsDataURL(this.files[0]);
            }
        });
    }

    // Format currency inputs
    document.querySelectorAll('.currency-input').forEach(function(input) {
        input.addEventListener('input', function(e) {
            // Remove non-numeric characters
            let value = this.value.replace(/[^0-9]/g, '');
            
            // Format with thousand separators
            if (value) {
                value = parseInt(value, 10).toLocaleString('vi-VN');
            }
            
            this.value = value;
        });
        
        // On form submit, convert to number
        input.form?.addEventListener('submit', function() {
            input.value = input.value.replace(/[^0-9]/g, '');
        });
    });

    // Datepicker initialization (if using bootstrap-datepicker)
    if (typeof $.fn.datepicker !== 'undefined') {
        $('.datepicker').datepicker({
            format: 'dd/mm/yyyy',
            autoclose: true,
            language: 'vi'
        });
    }

    // Select2 initialization (if using select2)
    if (typeof $.fn.select2 !== 'undefined') {
        $('.select2').select2({
            theme: 'bootstrap-5'
        });
    }
});

// Function to format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })
        .format(amount)
        .replace('₫', 'VNĐ');
}

// Function to format number with thousand separator
function formatNumber(number) {
    return new Intl.NumberFormat('vi-VN').format(number);
}