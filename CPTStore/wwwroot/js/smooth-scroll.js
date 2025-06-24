/**
 * Smooth Scroll Behavior
 * Xử lý vấn đề giật giật khi cuộn trang
 */
document.addEventListener('DOMContentLoaded', function() {
    // Biến để theo dõi thời gian cuộn cuối cùng
    let lastScrollTime = 0;
    // Biến để lưu trữ ID của timeout
    let scrollTimeoutId = null;
    // Biến để theo dõi vị trí cuộn trước đó
    let lastScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
    // Biến để theo dõi hướng cuộn
    let scrollDirection = 0;
    // Biến để theo dõi yêu cầu animation frame
    let ticking = false;
    
    // Hàm tiết chế (throttle) sự kiện cuộn với requestAnimationFrame
    function throttleScroll() {
        const currentScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
        
        // Xác định hướng cuộn
        scrollDirection = currentScrollPosition > lastScrollPosition ? 1 : -1;
        lastScrollPosition = currentScrollPosition;
        
        // Sử dụng requestAnimationFrame để đồng bộ với chu kỳ render của trình duyệt
        if (!ticking) {
            window.requestAnimationFrame(function() {
                handleScroll();
                ticking = false;
            });
            ticking = true;
        }
    }
    
    // Hàm xử lý sự kiện cuộn
    function handleScroll() {
        // Gọi tất cả các trình xử lý sự kiện cuộn đã đăng ký
        const handlers = window._existingScrollHandlers || [];
        handlers.forEach(handler => {
            if (typeof handler === 'function') {
                handler();
            }
        });
    }
    
    // Đăng ký sự kiện cuộn với hàm tiết chế và đặt passive thành true để cải thiện hiệu suất
    window.addEventListener('scroll', throttleScroll, { passive: true });
    
    // Vô hiệu hóa các sự kiện cuộn khác có thể gây ra vấn đề
    const existingScrollHandlers = window._existingScrollHandlers || [];
    if (existingScrollHandlers.length > 0) {
        existingScrollHandlers.forEach(handler => {
            if (handler !== throttleScroll) {
                window.removeEventListener('scroll', handler);
            }
        });
    }
    
    // Lưu trữ handler mới
    window._existingScrollHandlers = [throttleScroll];
    
    // Thêm CSS để cải thiện hiệu ứng cuộn mượt mà
    const style = document.createElement('style');
    style.textContent = `
        html {
            scroll-behavior: smooth;
        }
        body {
            overflow-x: hidden;
        }
        @media (prefers-reduced-motion: reduce) {
            html {
                scroll-behavior: auto;
            }
        }
    `;
    document.head.appendChild(style);
});