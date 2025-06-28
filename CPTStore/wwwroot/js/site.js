// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Xử lý sự kiện khi trang đã tải xong
document.addEventListener('DOMContentLoaded', function () {
    // Khởi tạo tooltips của Bootstrap
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl)
    })

    // Xử lý nút tăng/giảm số lượng sản phẩm
    setupQuantityButtons();

    // Xử lý form đánh giá sản phẩm
    setupReviewForm();

    // Xử lý cập nhật giỏ hàng
    setupCartUpdates();

    // Xử lý form thanh toán
    setupCheckoutForm();
});

// Write your code here

// Hàm xóa sản phẩm khỏi giỏ hàng
function removeCartItem(cartItemId) {
    // Gửi yêu cầu AJAX để xóa sản phẩm khỏi giỏ hàng
    fetch('/Cart/RemoveCartItem', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ cartItemId: cartItemId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            updateCartUI(data);
            // Xóa dòng sản phẩm khỏi bảng
            var cartItemRow = document.querySelector(`tr[data-cart-item-id="${cartItemId}"]`);
            if (cartItemRow) {
                cartItemRow.remove();
            }
            
            // Hiển thị thông báo thành công
            showNotification('success', data.message || 'Sản phẩm đã được xóa khỏi giỏ hàng');
        } else {
            // Hiển thị thông báo lỗi
            showNotification('error', data.message || 'Có lỗi xảy ra khi xóa sản phẩm khỏi giỏ hàng.');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('error', 'Có lỗi xảy ra khi xóa sản phẩm khỏi giỏ hàng.');
    });
}

// Hàm áp dụng mã giảm giá
function applyDiscount(discountCode) {
    // Gửi yêu cầu AJAX để áp dụng mã giảm giá
    fetch('/Cart/ApplyDiscountAjax', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ discountCode: discountCode })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            updateCartUI(data);
            // Hiển thị thông báo thành công
            showNotification('success', data.message || 'Mã giảm giá đã được áp dụng thành công!');
        } else {
            // Hiển thị thông báo lỗi
            showNotification('error', data.message || 'Mã giảm giá không hợp lệ hoặc đã hết hạn.');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('error', 'Có lỗi xảy ra khi áp dụng mã giảm giá.');
    });
}

// Hàm cập nhật giao diện giỏ hàng
function updateCartUI(data) {
    // Cập nhật tổng tiền
    var cartSubtotal = document.getElementById('cartSubtotal');
    var cartShipping = document.getElementById('cartShipping');
    var cartDiscount = document.getElementById('cartDiscount');
    var cartTotal = document.getElementById('cartTotal');
    
    if (cartSubtotal) cartSubtotal.textContent = data.subtotal;
    if (cartShipping) cartShipping.textContent = data.shippingFee;
    if (cartDiscount) cartDiscount.textContent = data.discount;
    if (cartTotal) cartTotal.textContent = data.total;
    
    // Cập nhật số lượng sản phẩm trong giỏ hàng
    var cartItemCount = document.querySelector('.badge');
    if (cartItemCount) {
        if (data.itemCount > 0) {
            cartItemCount.textContent = data.itemCount;
            cartItemCount.classList.remove('d-none');
        } else {
            cartItemCount.classList.add('d-none');
        }
    }
    
    // Kiểm tra nếu giỏ hàng trống
    if (data.itemCount === 0) {
        var cartTable = document.querySelector('.cart-table');
        var emptyCartMessage = document.querySelector('.empty-cart-message');
        
        if (cartTable) cartTable.classList.add('d-none');
        if (emptyCartMessage) emptyCartMessage.classList.remove('d-none');
    }
}

// Hàm thiết lập form thanh toán
function setupCheckoutForm() {
    // Xử lý chọn phương thức thanh toán
    var paymentMethods = document.querySelectorAll('input[name="PaymentMethod"]');
    if (paymentMethods.length > 0) {
        paymentMethods.forEach(method => {
            method.addEventListener('change', function() {
                // Hiển thị/ẩn các trường thông tin liên quan đến phương thức thanh toán
                if (this.value === 'BankTransfer') {
                    document.getElementById('bankInfo').classList.remove('d-none');
                } else {
                    document.getElementById('bankInfo').classList.add('d-none');
                }
                
                if (this.value === 'CreditCard') {
                    document.getElementById('creditCardInfo').classList.remove('d-none');
                } else {
                    document.getElementById('creditCardInfo').classList.add('d-none');
                }
                
                // Cập nhật tổng tiền nếu có phí xử lý thanh toán
                updatePaymentFee(this.value);
            });
        });
    }
    
    // Xử lý chọn phương thức vận chuyển
    var shippingMethods = document.querySelectorAll('input[name="ShippingMethod"]');
    if (shippingMethods.length > 0) {
        shippingMethods.forEach(method => {
            method.addEventListener('change', function() {
                // Cập nhật phí vận chuyển
                updateShippingFee(this.value);
            });
        });
    }
    
    // Xử lý form thanh toán khi submit
    var checkoutForm = document.getElementById('checkoutForm');
    if (checkoutForm) {
        checkoutForm.addEventListener('submit', function(e) {
            e.preventDefault();
            
            // Kiểm tra hợp lệ form
            if (validateCheckoutForm()) {
                // Gửi form
                this.submit();
            }
        });
    }
}

// Hàm cập nhật phí xử lý thanh toán
function updatePaymentFee(paymentMethod) {
    // Gửi yêu cầu AJAX để lấy phí xử lý thanh toán
    fetch('/Cart/GetPaymentFee', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ paymentMethod: paymentMethod })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            var paymentFeeElement = document.getElementById('paymentFee');
            var totalElement = document.getElementById('orderTotal');
            
            if (paymentFeeElement) {
                paymentFeeElement.textContent = data.paymentFee;
            }
            
            if (totalElement) {
                totalElement.textContent = data.total;
            }
        }
    })
    .catch(error => {
        console.error('Error:', error);
    });
}

// Hàm cập nhật phí vận chuyển
function updateShippingFee(shippingMethod) {
    // Gửi yêu cầu AJAX để lấy phí vận chuyển
    fetch('/Cart/GetShippingFee', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ shippingMethod: shippingMethod })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            var shippingFeeElement = document.getElementById('shippingFee');
            var totalElement = document.getElementById('orderTotal');
            
            if (shippingFeeElement) {
                shippingFeeElement.textContent = data.shippingFee;
            }
            
            if (totalElement) {
                totalElement.textContent = data.total;
            }
        }
    })
    .catch(error => {
        console.error('Error:', error);
    });
}

// Hàm kiểm tra hợp lệ form thanh toán
function validateCheckoutForm() {
    var isValid = true;
    var requiredFields = document.querySelectorAll('#checkoutForm [required]');
    
    // Xóa thông báo lỗi cũ
    document.querySelectorAll('.invalid-feedback').forEach(el => {
        el.style.display = 'none';
    });
    
    // Kiểm tra các trường bắt buộc
    requiredFields.forEach(field => {
        if (!field.value.trim()) {
            isValid = false;
            var feedbackEl = field.nextElementSibling;
            if (feedbackEl && feedbackEl.classList.contains('invalid-feedback')) {
                feedbackEl.style.display = 'block';
            }
        }
    });
    
    // Kiểm tra email hợp lệ
    var emailField = document.getElementById('email');
    if (emailField && emailField.value.trim() && !isValidEmail(emailField.value.trim())) {
        isValid = false;
        var feedbackEl = emailField.nextElementSibling;
        if (feedbackEl && feedbackEl.classList.contains('invalid-feedback')) {
            feedbackEl.style.display = 'block';
            feedbackEl.textContent = 'Vui lòng nhập địa chỉ email hợp lệ';
        }
    }
    
    // Kiểm tra số điện thoại hợp lệ
    var phoneField = document.getElementById('phone');
    if (phoneField && phoneField.value.trim() && !isValidPhone(phoneField.value.trim())) {
        isValid = false;
        var feedbackEl = phoneField.nextElementSibling;
        if (feedbackEl && feedbackEl.classList.contains('invalid-feedback')) {
            feedbackEl.style.display = 'block';
            feedbackEl.textContent = 'Vui lòng nhập số điện thoại hợp lệ';
        }
    }
    
    return isValid;
}

// Hàm kiểm tra email hợp lệ
function isValidEmail(email) {
    var re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
}

// Hàm kiểm tra số điện thoại hợp lệ
function isValidPhone(phone) {
    var re = /^[0-9]{10,11}$/;
    return re.test(phone);
}

// This section contains code that belongs to a function already defined above
document.addEventListener('DOMContentLoaded', function () {
    // Khởi tạo tooltips của Bootstrap
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl)
    })

    // Xử lý nút tăng/giảm số lượng sản phẩm
    setupQuantityButtons();

    // Xử lý form đánh giá sản phẩm
    setupReviewForm();

    // Xử lý cập nhật giỏ hàng
    setupCartUpdates();

    // Xử lý form thanh toán
    setupCheckoutForm();
});

// Hàm xử lý nút tăng/giảm số lượng sản phẩm
function setupQuantityButtons() {
    // Nút giảm số lượng
    document.querySelectorAll('.btn-quantity-decrease').forEach(function (button) {
        button.addEventListener('click', function () {
            var input = this.nextElementSibling;
            var value = parseInt(input.value);
            if (value > 1) {
                input.value = value - 1;
                // Kích hoạt sự kiện change để cập nhật tổng tiền nếu cần
                input.dispatchEvent(new Event('change'));
            }
        });
    });

    // Nút tăng số lượng
    document.querySelectorAll('.btn-quantity-increase').forEach(function (button) {
        button.addEventListener('click', function () {
            var input = this.previousElementSibling;
            var value = parseInt(input.value);
            var max = parseInt(input.getAttribute('max') || 99);
            if (value < max) {
                input.value = value + 1;
                // Kích hoạt sự kiện change để cập nhật tổng tiền nếu cần
                input.dispatchEvent(new Event('change'));
            }
        });
    });

    // Đảm bảo giá trị nhập vào là số hợp lệ
    document.querySelectorAll('.product-quantity').forEach(function (input) {
        input.addEventListener('change', function () {
            var value = parseInt(this.value);
            var min = parseInt(this.getAttribute('min') || 1);
            var max = parseInt(this.getAttribute('max') || 99);

            if (isNaN(value) || value < min) {
                this.value = min;
            } else if (value > max) {
                this.value = max;
            }
        });
    });
}

// Hàm xử lý form đánh giá sản phẩm
function setupReviewForm() {
    var reviewForm = document.querySelector('form[action*="AddReview"]');
    if (reviewForm) {
        reviewForm.addEventListener('submit', function (e) {
            var ratingInputs = document.querySelectorAll('input[name="Rating"]:checked');
            var commentText = document.getElementById('Comment').value.trim();

            // Kiểm tra xem đã chọn rating chưa
            if (ratingInputs.length === 0) {
                e.preventDefault();
                alert('Vui lòng chọn số sao đánh giá.');
                return false;
            }

            // Kiểm tra xem đã nhập nội dung đánh giá chưa
            if (commentText === '') {
                e.preventDefault();
                alert('Vui lòng nhập nội dung đánh giá.');
                return false;
            }

            return true;
        });
    }
}

// Hàm xử lý cập nhật giỏ hàng
function setupCartUpdates() {
    // Cập nhật số lượng sản phẩm trong giỏ hàng
    document.querySelectorAll('.cart-quantity-input').forEach(function (input) {
        input.addEventListener('change', function () {
            var cartItemId = this.getAttribute('data-cart-item-id');
            var quantity = parseInt(this.value);
            
            if (cartItemId && !isNaN(quantity) && quantity > 0) {
                updateCartItem(cartItemId, quantity);
            }
        });
    });

    // Xóa sản phẩm khỏi giỏ hàng
    document.querySelectorAll('.btn-remove-cart-item').forEach(function (button) {
        button.addEventListener('click', function () {
            var cartItemId = this.getAttribute('data-cart-item-id');
            if (cartItemId && confirm('Bạn có chắc chắn muốn xóa sản phẩm này khỏi giỏ hàng?')) {
                removeCartItem(cartItemId);
            }
        });
    });

    // Áp dụng mã giảm giá
    var applyDiscountForm = document.getElementById('applyDiscountForm');
    if (applyDiscountForm) {
        applyDiscountForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var discountCode = document.getElementById('discountCode').value.trim();
            if (discountCode) {
                applyDiscount(discountCode);
            } else {
                alert('Vui lòng nhập mã giảm giá.');
            }
        });
    }
}

// Hàm cập nhật số lượng sản phẩm trong giỏ hàng
function updateCartItem(cartItemId, quantity) {
    // Gửi yêu cầu AJAX để cập nhật giỏ hàng
    fetch('/Cart/UpdateCartItem', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ cartItemId: cartItemId, quantity: quantity })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            updateCartUI(data);
        } else {
            alert(data.message || 'Có lỗi xảy ra khi cập nhật giỏ hàng.');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        alert('Có lỗi xảy ra khi cập nhật giỏ hàng.');
    });
}

// This section contains functions already defined above

// This code is already defined in the setupCheckoutForm and validateCheckoutForm functions above

// Hàm cập nhật phí vận chuyển
function updateShippingCost(shippingMethod) {
    // Gửi yêu cầu AJAX để cập nhật phí vận chuyển
    fetch('/Order/UpdateShippingCost', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ shippingMethod: shippingMethod })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Cập nhật UI
            var orderShipping = document.getElementById('orderShipping');
            var orderTotal = document.getElementById('orderTotal');
            if (orderShipping) orderShipping.textContent = data.shipping;
            if (orderTotal) orderTotal.textContent = data.total;
        }
    })
    .catch(error => {
        console.error('Error:', error);
    });
}

// This section contains functions already defined above
