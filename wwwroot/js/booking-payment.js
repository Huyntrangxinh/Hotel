// Booking Payment Handler
window.BookingPayment = {
    showPaymentForm: function (bookingInfo) {
        // Luôn lấy thông tin mới nhất từ localStorage để đảm bảo có roomPriceAmount
        const storedInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
        if (storedInfo) {
            // Merge thông tin từ localStorage vào bookingInfo (ưu tiên storedInfo)
            bookingInfo = Object.assign({}, bookingInfo, storedInfo);
        }

        // Nếu vẫn không có roomPriceAmount, thử lấy từ window
        if (!bookingInfo.roomPriceAmount && window.currentRoomPriceAmount) {
            bookingInfo.roomPriceAmount = window.currentRoomPriceAmount;
        }

        // Đảm bảo roomPriceAmount là number
        if (bookingInfo.roomPriceAmount) {
            bookingInfo.roomPriceAmount = parseFloat(bookingInfo.roomPriceAmount);
        }

        console.log('showPaymentForm - bookingInfo:', bookingInfo);
        console.log('showPaymentForm - roomPriceAmount:', bookingInfo.roomPriceAmount);
        console.log('showPaymentForm - getRoomPricePerNight:', BookingPayment.getRoomPricePerNight(bookingInfo));

        // Lấy giá phòng trước khi tạo HTML
        const roomPricePerNight = BookingPayment.getRoomPricePerNight(bookingInfo);
        const nights = BookingPayment.calculateNights(bookingInfo.checkIn, bookingInfo.checkOut);
        const subtotal = nights * roomPricePerNight;
        const tax = Math.round(subtotal * 0.1);
        const total = subtotal + tax;

        console.log('showPaymentForm - Calculated values:', {
            roomPricePerNight,
            nights,
            subtotal,
            tax,
            total
        });

        // Xác định form hiện tại
        let formRoot = null;
        if (bookingInfo && bookingInfo.formId) {
            formRoot = document.getElementById(bookingInfo.formId);
        }
        if (!formRoot) {
            formRoot = document.querySelector('div[style*="margin-top: 20px; padding: 20px; background: rgba(255, 255, 255, 0.9)"]');
        }

        // Ẩn phần tóm tắt đã thu thập
        if (formRoot) {
            const summaryDiv = formRoot.querySelector('.booking-summary');
            if (summaryDiv) {
                summaryDiv.style.display = 'none';
            }
        }

        // Tạo HTML cho form thanh toán - sử dụng giá đã tính toán
        const paymentFormHtml = `
            <div id="paymentForm" style="margin-top: 20px; padding: 20px; background: rgba(255, 255, 255, 0.9); border-radius: 12px; border: 1px solid rgba(59, 130, 246, 0.2); box-shadow: 0 4px 15px rgba(59, 130, 246, 0.1); backdrop-filter: blur(10px);">
                <h4 style="color: #1e40af; margin-bottom: 20px; display: flex; align-items: center;">
                    <span style="margin-right: 10px;">💳</span>
                    Chọn phương thức thanh toán
                </h4>
                
                <div style="background: #f0f9ff; padding: 15px; border-radius: 8px; margin-bottom: 20px; border-left: 4px solid #3b82f6;">
                    <h5 style="color: #1e40af; margin: 0 0 10px 0;">📋 Hóa đơn đặt phòng</h5>
                    <div style="font-size: 14px; color: #374151;">
                        <div style="margin-bottom: 5px;"><strong>👤 Khách hàng:</strong> ${bookingInfo.fullName}</div>
                        <div style="margin-bottom: 5px;"><strong>📱 Điện thoại:</strong> ${bookingInfo.phone}</div>
                        <div style="margin-bottom: 5px;"><strong>📧 Email:</strong> ${bookingInfo.email}</div>
                        <div style="margin-bottom: 5px;"><strong>📅 Nhận phòng:</strong> ${bookingInfo.checkIn}</div>
                        <div style="margin-bottom: 5px;"><strong>📅 Trả phòng:</strong> ${bookingInfo.checkOut}</div>
                        <div style="margin-bottom: 5px;"><strong>👥 Số khách:</strong> ${bookingInfo.guests} người</div>
                        ${bookingInfo.specialRequests ? `<div style="margin-bottom: 5px;"><strong>💬 Yêu cầu đặc biệt:</strong> ${bookingInfo.specialRequests}</div>` : ''}
                        <div style="margin-top: 10px; padding-top: 10px; border-top: 1px solid #d1d5db;">
                            <div style="font-size: 14px; color: #374151; margin-bottom: 8px;">
                                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                                    <span>💰 Giá phòng/đêm:</span>
                                    <span>${roomPricePerNight.toLocaleString('vi-VN')} VND</span>
                                </div>
                                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                                    <span>📅 Số đêm:</span>
                                    <span>${nights} đêm</span>
                                </div>
                                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                                    <span>🧮 Tạm tính:</span>
                                    <span>${subtotal.toLocaleString('vi-VN')} VND</span>
                                </div>
                                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                                    <span>💸 Thuế (10%):</span>
                                    <span>${tax.toLocaleString('vi-VN')} VND</span>
                                </div>
                            </div>
                            <div style="font-size: 16px; font-weight: bold; color: #1e40af; border-top: 1px solid #d1d5db; padding-top: 8px;">
                                💰 Tổng tiền: ${total.toLocaleString('vi-VN')} VND
                            </div>
                        </div>
                    </div>
                </div>
                
                <div style="margin-bottom: 20px;">
                    <h5 style="color: #1e40af; margin-bottom: 15px;">💳 Bạn muốn thanh toán thế nào?</h5>
                    <div style="display: grid; gap: 10px;">
                        <label style="display: flex; align-items: center; padding: 12px; border: 2px solid #3b82f6; border-radius: 8px; cursor: pointer; background: #eff6ff; transition: all 0.3s;" onclick="BookingPayment.selectPaymentMethod('vietqr', this)">
                            <input type="radio" name="paymentMethod" value="vietqr" checked style="margin-right: 10px;">
                            <span style="font-weight: 600;">📱 VietQR</span>
                        </label>
                        <label style="display: flex; align-items: center; padding: 12px; border: 2px solid #e5e7eb; border-radius: 8px; cursor: pointer; transition: all 0.3s;" onclick="BookingPayment.selectPaymentMethod('mobilebanking', this)">
                            <input type="radio" name="paymentMethod" value="mobilebanking" style="margin-right: 10px;">
                            <span style="font-weight: 600;">🏦 Ngân hàng di động</span>
                        </label>
                        <label style="display: flex; align-items: center; padding: 12px; border: 2px solid #e5e7eb; border-radius: 8px; cursor: pointer; transition: all 0.3s;" onclick="BookingPayment.selectPaymentMethod('store', this)">
                            <input type="radio" name="paymentMethod" value="store" style="margin-right: 10px;">
                            <span style="font-weight: 600;">🏪 Tại cửa hàng</span>
                        </label>
                        <label style="display: flex; align-items: center; padding: 12px; border: 2px solid #e5e7eb; border-radius: 8px; cursor: pointer; transition: all 0.3s;" onclick="BookingPayment.selectPaymentMethod('card', this)">
                            <input type="radio" name="paymentMethod" value="card" style="margin-right: 10px;">
                            <span style="font-weight: 600;">💳 Thẻ thanh toán</span>
                        </label>
                    </div>
                </div>
                
                <div id="paymentDetails" style="margin-bottom: 20px; padding: 20px; background: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0;">
                    <div id="vietqrDetails" style="display: block;">
                        <h6 style="color: #1e40af; margin-bottom: 15px; display: flex; align-items: center;">
                            <span style="margin-right: 8px;">📱</span>
                            Quét mã QR để thanh toán
                        </h6>
                        <div style="text-align: center; margin-bottom: 15px;">
                            <div id="qrCode" style="display: inline-block; padding: 20px; background: white; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                                <div id="qrCodeContent" style="width: 200px; height: 200px; display: flex; align-items: center; justify-content: center;">
                                    <img src="/images/qrcode.png" alt="QR Code" style="width: 200px; height: 200px; object-fit: contain;" />
                                </div>
                            </div>
                        </div>
                        <p style="text-align: center; color: #64748b; font-size: 14px; margin-bottom: 10px;">
                            Mở ứng dụng ngân hàng và quét mã QR để thanh toán
                        </p>
                        <div style="text-align: center; font-weight: bold; color: #1e40af; font-size: 16px;">
                            Số tiền: ${total.toLocaleString('vi-VN')} VND
                        </div>
                    </div>
                    
                    <div id="mobilebankingDetails" style="display: none;">
                        <h6 style="color: #1e40af; margin-bottom: 15px;">🏦 Ngân hàng di động</h6>
                        <p style="color: #64748b; margin-bottom: 15px;">Chuyển khoản qua ứng dụng ngân hàng di động</p>
                        <div style="background: white; padding: 15px; border-radius: 6px; border: 1px solid #e2e8f0;">
                            <div style="margin-bottom: 10px;"><strong>STK:</strong> 1234567890</div>
                            <div style="margin-bottom: 10px;"><strong>Ngân hàng:</strong> Vietcombank</div>
                            <div style="margin-bottom: 10px;"><strong>Chủ TK:</strong> Hotel Booking System</div>
                            <div style="font-weight: bold; color: #1e40af;">Số tiền: ${total.toLocaleString('vi-VN')} VND</div>
                        </div>
                    </div>
                    
                    <div id="storeDetails" style="display: none;">
                        <h6 style="color: #1e40af; margin-bottom: 15px;">🏪 Tại cửa hàng</h6>
                        <p style="color: #64748b; margin-bottom: 15px;">Thanh toán tại các cửa hàng tiện lợi</p>
                        <div style="background: white; padding: 15px; border-radius: 6px; border: 1px solid #e2e8f0;">
                            <div style="margin-bottom: 10px;">• Circle K, FamilyMart, 7-Eleven</div>
                            <div style="margin-bottom: 10px;">• Mã thanh toán: <strong>${Math.random().toString(36).substr(2, 8).toUpperCase()}</strong></div>
                            <div style="font-weight: bold; color: #1e40af;">Số tiền: ${total.toLocaleString('vi-VN')} VND</div>
                        </div>
                    </div>
                    
                    <div id="cardDetails" style="display: none;">
                        <h6 style="color: #1e40af; margin-bottom: 15px;">💳 Thẻ thanh toán</h6>
                        <p style="color: #64748b; margin-bottom: 15px;">Thanh toán bằng thẻ tín dụng/ghi nợ</p>
                        <div style="background: white; padding: 15px; border-radius: 6px; border: 1px solid #e2e8f0;">
                            <div style="margin-bottom: 10px;">• Visa, Mastercard, JCB</div>
                            <div style="margin-bottom: 10px;">• Bảo mật SSL 256-bit</div>
                            <div style="font-weight: bold; color: #1e40af;">Số tiền: ${total.toLocaleString('vi-VN')} VND</div>
                        </div>
                    </div>
                </div>
                
                <div style="text-align: center; margin-top: 20px;">
                    <button onclick="BookingPayment.processPayment()" style="background: #3b82f6; color: white; padding: 15px 40px; border: none; border-radius: 8px; font-weight: bold; cursor: pointer; font-size: 16px; width: 100%; max-width: 300px; box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3); transition: all 0.3s;" onmouseover="this.style.background='#2563eb'; this.style.transform='translateY(-2px)'" onmouseout="this.style.background='#3b82f6'; this.style.transform='translateY(0)'">
                        💳 Thanh toán & xác nhận
                    </button>
                    <button onclick="BookingPayment.goBackToBooking()" style="background: transparent; color: #6b7280; padding: 10px 20px; border: 1px solid #d1d5db; border-radius: 6px; font-weight: 500; cursor: pointer; font-size: 14px; margin-top: 10px;">
                        ← Quay lại
                    </button>
                </div>
            </div>
        `;

        // Thêm form thanh toán vào DOM
        const existingPaymentForm = document.getElementById('paymentForm');
        if (existingPaymentForm) {
            existingPaymentForm.remove();
        }

        if (formRoot) {
            formRoot.insertAdjacentHTML('afterend', paymentFormHtml);
            formRoot.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } else {
            // Nếu không tìm thấy form, thêm vào cuối chatbot
            const chatbotMessages = document.querySelector('.chatbot-messages') || document.body;
            chatbotMessages.insertAdjacentHTML('beforeend', paymentFormHtml);
        }
    },

    processPayment: function () {
        const selectedPayment = document.querySelector('input[name="paymentMethod"]:checked');
        if (!selectedPayment) {
            alert('Vui lòng chọn phương thức thanh toán');
            return;
        }

        const bookingInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
        const paymentMethod = selectedPayment.value;

        // Hiển thị màn hình thành công thay vì chuyển hướng
        BookingPayment.showBookingSuccess(bookingInfo);
    },

    goBackToBooking: function () {
        // Ẩn form thanh toán và hiển thị lại form thu thập thông tin
        const paymentForm = document.getElementById('paymentForm');
        if (paymentForm) {
            paymentForm.remove();
        }
        const bookingInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
        let formRoot = null;
        if (bookingInfo && bookingInfo.formId) {
            formRoot = document.getElementById(bookingInfo.formId);
        }
        if (!formRoot) {
            formRoot = document.querySelector('div[style*="margin-top: 20px; padding: 20px; background: rgba(255, 255, 255, 0.9)"]');
        }
        const summaryDiv = formRoot ? formRoot.querySelector('.booking-summary') : null;
        if (summaryDiv) summaryDiv.style.display = 'block';
    }
};

// Override confirmBooking function to use the new payment form
window.confirmBooking = function () {
    const bookingInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
    console.log('confirmBooking - bookingInfo from localStorage:', bookingInfo);
    console.log('confirmBooking - roomPriceAmount:', bookingInfo.roomPriceAmount);

    if (bookingInfo.propertyId) {
        // Đảm bảo bookingInfo có đầy đủ thông tin từ localStorage
        if (!bookingInfo.roomPriceAmount) {
            console.log('confirmBooking - roomPriceAmount missing, trying to get from window.currentRoomPriceAmount');
            if (window.currentRoomPriceAmount) {
                bookingInfo.roomPriceAmount = window.currentRoomPriceAmount;
            }
        }

        // Hiển thị form thanh toán thay vì chuyển hướng
        BookingPayment.showPaymentForm(bookingInfo);
    }
};

// Thêm các hàm tính toán
BookingPayment.calculateNights = function (checkIn, checkOut) {
    const checkInDate = new Date(checkIn);
    const checkOutDate = new Date(checkOut);
    const timeDiff = checkOutDate.getTime() - checkInDate.getTime();
    const nights = Math.ceil(timeDiff / (1000 * 3600 * 24));
    return nights > 0 ? nights : 1; // Tối thiểu 1 đêm
};

BookingPayment.getRoomPricePerNight = function (bookingInfo) {
    // Lấy giá từ bookingInfo nếu có, nếu không thì dùng giá mặc định
    if (bookingInfo) {
        // Thử lấy từ roomPriceAmount
        if (bookingInfo.roomPriceAmount) {
            const price = parseFloat(bookingInfo.roomPriceAmount);
            if (!isNaN(price) && price > 0) {
                console.log('Using roomPriceAmount from bookingInfo:', price);
                return price;
            }
        }
        // Thử lấy từ localStorage nếu bookingInfo không có
        const storedInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
        if (storedInfo.roomPriceAmount) {
            const price = parseFloat(storedInfo.roomPriceAmount);
            if (!isNaN(price) && price > 0) {
                console.log('Using roomPriceAmount from localStorage:', price);
                return price;
            }
        }
    }
    // Fallback: thử lấy từ window.currentRoomPriceAmount nếu có
    if (window.currentRoomPriceAmount) {
        const price = parseFloat(window.currentRoomPriceAmount);
        if (!isNaN(price) && price > 0) {
            console.log('Using currentRoomPriceAmount from window:', price);
            return price;
        }
    }
    // Giá mặc định
    console.log('Using default price: 2000000');
    return 2000000;
};

BookingPayment.calculateTotalAmountValue = function (checkIn, checkOut, bookingInfo) {
    const nights = BookingPayment.calculateNights(checkIn, checkOut);
    const roomPricePerNight = BookingPayment.getRoomPricePerNight(bookingInfo || {});
    const subtotal = nights * roomPricePerNight;
    const tax = Math.round(subtotal * 0.1); // 10% thuế
    return subtotal + tax;
};

BookingPayment.calculateTotalAmount = function (checkIn, checkOut, bookingInfo) {
    return BookingPayment.calculateTotalAmountValue(checkIn, checkOut, bookingInfo).toLocaleString('vi-VN');
};

BookingPayment.selectPaymentMethod = function (method, element) {
    // Reset all labels
    const allLabels = document.querySelectorAll('label[onclick*="selectPaymentMethod"]');
    allLabels.forEach(label => {
        label.style.borderColor = '#e5e7eb';
        label.style.background = 'transparent';
    });

    // Highlight selected method
    element.style.borderColor = '#3b82f6';
    element.style.background = '#eff6ff';

    // Hide all payment details
    const allDetails = ['vietqrDetails', 'mobilebankingDetails', 'storeDetails', 'cardDetails'];
    allDetails.forEach(id => {
        const detail = document.getElementById(id);
        if (detail) detail.style.display = 'none';
    });

    // Show selected payment details
    const selectedDetail = document.getElementById(method + 'Details');
    if (selectedDetail) {
        selectedDetail.style.display = 'block';
    }

    // Update radio button
    const radio = element.querySelector('input[type="radio"]');
    if (radio) {
        radio.checked = true;
    }

    // Nếu chọn "Thẻ thanh toán", hiển thị form nhập thông tin thẻ
    if (method === 'Thẻ thanh toán') {
        BookingPayment.showCardPaymentForm();
    }
};

// Sử dụng ảnh QR code có sẵn từ /images/qrcode.png

// Xử lý thẻ thanh toán
BookingPayment.showCardPaymentForm = function () {
    // Tạo form nhập thông tin thẻ thanh toán
    const cardFormHtml = `
        <div id="cardPaymentForm" style="margin-top: 15px; padding: 20px; background: rgba(255, 255, 255, 0.95); border-radius: 8px; border: 1px solid #e5e7eb; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
            <h5 style="color: #1e40af; margin-bottom: 15px; display: flex; align-items: center;">
                <span style="margin-right: 8px;">💳</span>
                Thông tin thẻ thanh toán
            </h5>
            
            <div style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">Loại thẻ</label>
                <select id="cardType" style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px; background: white;">
                    <option value="">Chọn loại thẻ</option>
                    <option value="visa">Visa</option>
                    <option value="mastercard">Mastercard</option>
                    <option value="jcb">JCB</option>
                </select>
            </div>

            <div style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">Số thẻ</label>
                <input type="text" id="cardNumber" placeholder="1234 5678 9012 3456" 
                       style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px; font-family: monospace;"
                       maxlength="19" oninput="this.value = this.value.replace(/\D/g, '').replace(/(.{4})/g, '$1 ').trim()">
            </div>

            <div style="display: flex; gap: 10px; margin-bottom: 15px;">
                <div style="flex: 1;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">Ngày hết hạn</label>
                    <input type="text" id="expiryDate" placeholder="MM/YY" 
                           style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px;"
                           maxlength="5" oninput="this.value = this.value.replace(/\D/g, '').replace(/(.{2})/g, '$1/').trim()">
                </div>
                <div style="flex: 1;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">CVV</label>
                    <input type="text" id="cvv" placeholder="123" 
                           style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px;"
                           maxlength="4" oninput="this.value = this.value.replace(/\D/g, '')">
                </div>
            </div>

            <div style="margin-bottom: 15px;">
                <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">Tên chủ thẻ</label>
                <input type="text" id="cardholderName" placeholder="NGUYEN VAN A" 
                       style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px; text-transform: uppercase;">
            </div>

            <div style="margin-bottom: 20px;">
                <label style="display: block; margin-bottom: 5px; font-weight: 500; color: #374151;">Địa chỉ thanh toán</label>
                <input type="text" id="billingAddress" placeholder="Nhập địa chỉ thanh toán" 
                       style="width: 100%; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px;">
            </div>

            <div style="background: #f0f9ff; padding: 12px; border-radius: 6px; margin-bottom: 15px; border-left: 4px solid #3b82f6;">
                <div style="font-size: 14px; color: #1e40af; margin-bottom: 8px;">
                    <strong>🔒 Bảo mật thanh toán</strong>
                </div>
                <div style="font-size: 13px; color: #64748b;">
                    • Mã hóa SSL 256-bit<br>
                    • Không lưu trữ thông tin thẻ<br>
                    • Tuân thủ tiêu chuẩn PCI DSS
                </div>
            </div>

            <div style="display: flex; gap: 10px;">
                <button onclick="BookingPayment.processCardPayment()" 
                        style="flex: 1; background: #3b82f6; color: white; border: none; padding: 12px; border-radius: 6px; font-weight: 500; cursor: pointer; transition: background 0.2s;">
                    💳 Thanh toán ngay
                </button>
                <button onclick="BookingPayment.cancelCardPayment()" 
                        style="background: #6b7280; color: white; border: none; padding: 12px; border-radius: 6px; font-weight: 500; cursor: pointer; transition: background 0.2s;">
                    ❌ Hủy
                </button>
            </div>
        </div>
    `;

    // Thêm form vào DOM
    const paymentForm = document.getElementById('paymentForm');
    if (paymentForm) {
        // Xóa form cũ nếu có
        const existingCardForm = document.getElementById('cardPaymentForm');
        if (existingCardForm) {
            existingCardForm.remove();
        }

        paymentForm.insertAdjacentHTML('beforeend', cardFormHtml);
    }
};

BookingPayment.processCardPayment = function () {
    // Lấy thông tin từ form
    const cardType = document.getElementById('cardType').value;
    const cardNumber = document.getElementById('cardNumber').value;
    const expiryDate = document.getElementById('expiryDate').value;
    const cvv = document.getElementById('cvv').value;
    const cardholderName = document.getElementById('cardholderName').value;
    const billingAddress = document.getElementById('billingAddress').value;

    // Validation
    if (!cardType) {
        alert('Vui lòng chọn loại thẻ');
        return;
    }
    if (!cardNumber || cardNumber.replace(/\s/g, '').length < 16) {
        alert('Vui lòng nhập số thẻ hợp lệ (16 chữ số)');
        return;
    }
    if (!expiryDate || expiryDate.length < 5) {
        alert('Vui lòng nhập ngày hết hạn (MM/YY)');
        return;
    }
    if (!cvv || cvv.length < 3) {
        alert('Vui lòng nhập CVV (3-4 chữ số)');
        return;
    }
    if (!cardholderName.trim()) {
        alert('Vui lòng nhập tên chủ thẻ');
        return;
    }
    if (!billingAddress.trim()) {
        alert('Vui lòng nhập địa chỉ thanh toán');
        return;
    }

    // Hiển thị thông báo xử lý
    const cardForm = document.getElementById('cardPaymentForm');
    if (cardForm) {
        cardForm.innerHTML = `
            <div style="text-align: center; padding: 20px;">
                <div style="font-size: 24px; margin-bottom: 10px;">⏳</div>
                <div style="color: #1e40af; font-weight: 500; margin-bottom: 10px;">Đang xử lý thanh toán...</div>
                <div style="color: #64748b; font-size: 14px;">Vui lòng đợi trong giây lát</div>
            </div>
        `;
    }

    // Giả lập xử lý thanh toán
    setTimeout(() => {
        BookingPayment.showPaymentSuccess();
    }, 2000);
};

BookingPayment.showPaymentSuccess = function () {
    const cardForm = document.getElementById('cardPaymentForm');
    if (cardForm) {
        cardForm.innerHTML = `
            <div style="text-align: center; padding: 20px;">
                <div style="font-size: 48px; margin-bottom: 15px; color: #10b981;">✅</div>
                <div style="color: #10b981; font-weight: 600; font-size: 18px; margin-bottom: 10px;">Thanh toán thành công!</div>
                <div style="color: #64748b; font-size: 14px; margin-bottom: 20px;">Đơn đặt phòng của bạn đã được xác nhận</div>
                <div style="background: #f0f9ff; padding: 12px; border-radius: 6px; margin-bottom: 15px; border-left: 4px solid #3b82f6;">
                    <div style="font-size: 14px; color: #1e40af; margin-bottom: 5px;">
                        <strong>📧 Email xác nhận đã được gửi</strong>
                    </div>
                    <div style="font-size: 13px; color: #64748b;">
                        Vui lòng kiểm tra hộp thư để nhận thông tin chi tiết về đơn đặt phòng
                    </div>
                </div>
                <button onclick="BookingPayment.completeBooking()" 
                        style="background: #10b981; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-weight: 500; cursor: pointer;">
                    🏠 Về trang chủ
                </button>
            </div>
        `;
    }
};

BookingPayment.cancelCardPayment = function () {
    const cardForm = document.getElementById('cardPaymentForm');
    if (cardForm) {
        cardForm.remove();
    }
};

BookingPayment.completeBooking = function () {
    // Chuyển về trang chủ
    window.location.href = '/';
};

// Gửi email xác nhận đặt phòng
BookingPayment.sendConfirmationEmail = function (bookingInfo, bookingId) {
    const emailData = {
        email: bookingInfo.email || 'test@example.com',
        fullName: bookingInfo.fullName || 'Nguyễn Văn A',
        phone: bookingInfo.phone || '0123456789',
        checkIn: bookingInfo.checkIn || '25/12/2024',
        checkOut: bookingInfo.checkOut || '28/12/2024',
        guests: bookingInfo.guests || '2',
        bookingId: bookingId,
        hotelName: bookingInfo.propertyName || 'Khách sạn bạn đã chọn',
        roomType: bookingInfo.roomName || 'Hạng phòng',
        propertyId: bookingInfo.propertyId,
        roomId: bookingInfo.roomId,
        propertyName: bookingInfo.propertyName || '',
        roomName: bookingInfo.roomName || '',
        specialRequests: bookingInfo.specialRequests || '',
        totalAmount: BookingPayment.calculateTotalAmountValue(bookingInfo.checkIn, bookingInfo.checkOut, bookingInfo).toString()
    };

    fetch('/Chat/SendBookingConfirmationEmail', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(emailData)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                console.log('Email xác nhận đã được gửi thành công!');
                // Hiển thị thông báo thành công trong chatbot
                BookingPayment.showEmailNotification('✅ Email xác nhận đã được gửi đến ' + emailData.email);
            } else {
                console.error('Lỗi gửi email:', data.message);
                BookingPayment.showEmailNotification('❌ Có lỗi khi gửi email: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error sending email:', error);
            BookingPayment.showEmailNotification('❌ Có lỗi xảy ra khi gửi email xác nhận');
        });
};

// Hiển thị thông báo email trong chatbot
BookingPayment.showEmailNotification = function (message) {
    const chatbotMessages = document.querySelector('.chatbot-messages');
    if (chatbotMessages) {
        const notification = document.createElement('div');
        notification.className = 'bot-message';
        notification.innerHTML = `
            <div style="background: rgba(59, 130, 246, 0.1); border: 1px solid #3b82f6; border-radius: 8px; padding: 15px; margin: 10px 0; text-align: center;">
                <div style="color: #1e40af; font-weight: 500;">${message}</div>
            </div>
        `;

        chatbotMessages.appendChild(notification);
        chatbotMessages.scrollTop = chatbotMessages.scrollHeight;
    }
};

// Hiển thị màn hình thành công giống /Booking/Success
BookingPayment.showBookingSuccess = function (bookingInfo) {
    // Tạo mã đặt phòng ngẫu nhiên
    const bookingId = 'KP' + Math.random().toString(36).substr(2, 6).toUpperCase();

    const successHtml = `
        <div id="bookingSuccess" style="margin-top: 20px; padding: 30px; background: rgba(255, 255, 255, 0.95); border-radius: 12px; border: 1px solid #e5e7eb; box-shadow: 0 4px 15px rgba(0,0,0,0.1); text-align: center;">
            <div style="margin-bottom: 20px;">
                <div style="font-size: 48px; color: #10b981; margin-bottom: 15px;">✅</div>
                <h2 style="color: #1f2937; margin-bottom: 10px; font-size: 28px; font-weight: 600;">Đặt phòng thành công!</h2>
                <p style="color: #6b7280; font-size: 16px; margin-bottom: 20px;">
                    Cảm ơn bạn đã đặt phòng. Chúng tôi sẽ gửi email xác nhận đến địa chỉ email của bạn.
                </p>
            </div>

            <div style="background: #f8fafc; padding: 20px; border-radius: 8px; margin-bottom: 25px; border-left: 4px solid #3b82f6;">
                <h5 style="color: #1f2937; margin-bottom: 10px; font-size: 18px;">Mã đặt phòng</h5>
                <p style="color: #3b82f6; font-size: 24px; font-weight: bold; margin: 0;">${bookingId}</p>
            </div>

            <div style="display: flex; gap: 20px; margin-bottom: 25px; text-align: left;">
                <div style="flex: 1;">
                    <h6 style="color: #1f2937; margin-bottom: 15px; font-size: 16px;">📋 Bước tiếp theo:</h6>
                    <ul style="list-style: none; padding: 0; margin: 0;">
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">📧</span>
                            Kiểm tra email xác nhận
                        </li>
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">📞</span>
                            Nhân viên sẽ liên hệ xác nhận
                        </li>
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">📅</span>
                            Đến khách sạn theo ngày đã đặt
                        </li>
                    </ul>
                </div>
                <div style="flex: 1;">
                    <h6 style="color: #1f2937; margin-bottom: 15px; font-size: 16px;">🛟 Hỗ trợ khách hàng:</h6>
                    <ul style="list-style: none; padding: 0; margin: 0;">
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">📞</span>
                            Hotline: 1900 1234
                        </li>
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">📧</span>
                            Email: support@hotelbooking.com
                        </li>
                        <li style="margin-bottom: 10px; color: #4b5563; font-size: 14px;">
                            <span style="color: #3b82f6; margin-right: 8px;">🕐</span>
                            24/7 hỗ trợ
                        </li>
                    </ul>
                </div>
            </div>

            <div style="display: flex; gap: 15px; justify-content: center; margin-bottom: 20px;">
                <button onclick="BookingPayment.downloadInvoice('${bookingId}', '${bookingInfo.fullName}', '${bookingInfo.checkIn}', '${bookingInfo.checkOut}', '${bookingInfo.guests}')" 
                        style="background: #3b82f6; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-weight: 500; cursor: pointer; display: flex; align-items: center; gap: 8px;">
                    📄 Tải hóa đơn PDF
                </button>
                <button onclick="BookingPayment.completeBooking()" 
                        style="background: #10b981; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-weight: 500; cursor: pointer; display: flex; align-items: center; gap: 8px;">
                    🏠 Về trang chủ
                </button>
            </div>

            <div style="background: #f0f9ff; padding: 15px; border-radius: 6px; border-left: 4px solid #3b82f6;">
                <div style="color: #1e40af; font-size: 14px; font-weight: 500; margin-bottom: 5px;">
                    📧 Email xác nhận đã được gửi
                </div>
                <div style="color: #64748b; font-size: 13px;">
                    Vui lòng kiểm tra hộp thư để nhận thông tin chi tiết về đơn đặt phòng
                </div>
            </div>
        </div>
    `;

    // Thêm màn hình thành công vào chatbot thay vì thay thế form
    const chatbotMessages = document.querySelector('.chatbot-messages');
    if (chatbotMessages) {
        // Tạo message mới cho bot
        const botMessage = document.createElement('div');
        botMessage.className = 'bot-message';
        botMessage.innerHTML = successHtml;

        // Thêm vào cuối danh sách messages
        chatbotMessages.appendChild(botMessage);

        // Scroll xuống cuối
        chatbotMessages.scrollTop = chatbotMessages.scrollHeight;
    }

    // Ẩn form thanh toán thay vì thay thế
    const paymentForm = document.getElementById('paymentForm');
    if (paymentForm) {
        paymentForm.style.display = 'none';
    }

    // Gửi email xác nhận
    BookingPayment.sendConfirmationEmail(bookingInfo, bookingId);
};

// Tạo và tải hóa đơn PDF
BookingPayment.downloadInvoice = function (bookingId, fullName, checkIn, checkOut, guests) {
    // Lấy bookingInfo từ localStorage
    const bookingInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{}');
    // Tạo nội dung HTML cho PDF
    const invoiceHtml = `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>Hóa đơn đặt phòng - ${bookingId}</title>
            <style>
                body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background: white; }
                .header { text-align: center; margin-bottom: 30px; border-bottom: 2px solid #3b82f6; padding-bottom: 20px; }
                .logo { font-size: 24px; font-weight: bold; color: #3b82f6; margin-bottom: 10px; }
                .invoice-title { font-size: 20px; color: #1f2937; margin-bottom: 5px; }
                .booking-id { font-size: 16px; color: #6b7280; }
                .content { margin-bottom: 30px; }
                .section { margin-bottom: 20px; }
                .section-title { font-size: 16px; font-weight: bold; color: #1f2937; margin-bottom: 10px; border-bottom: 1px solid #e5e7eb; padding-bottom: 5px; }
                .info-row { display: flex; margin-bottom: 8px; }
                .info-label { font-weight: bold; color: #374151; width: 150px; }
                .info-value { color: #6b7280; }
                .summary { background: #f8fafc; padding: 20px; border-radius: 8px; border: 1px solid #e5e7eb; }
                .total { font-size: 18px; font-weight: bold; color: #1f2937; text-align: right; margin-top: 15px; padding-top: 15px; border-top: 2px solid #3b82f6; }
                .footer { text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e5e7eb; color: #6b7280; font-size: 14px; }
            </style>
        </head>
        <body>
            <div class="header">
                <div class="logo">🏨 HotelBooking.com</div>
                <div class="invoice-title">HÓA ĐƠN ĐẶT PHÒNG</div>
                <div class="booking-id">Mã đặt phòng: ${bookingId}</div>
            </div>

            <div class="content">
                <div class="section">
                    <div class="section-title">📋 Thông tin khách hàng</div>
                    <div class="info-row">
                        <div class="info-label">Họ tên:</div>
                        <div class="info-value">${fullName}</div>
                    </div>
                    <div class="info-row">
                        <div class="info-label">Ngày nhận phòng:</div>
                        <div class="info-value">${checkIn}</div>
                    </div>
                    <div class="info-row">
                        <div class="info-label">Ngày trả phòng:</div>
                        <div class="info-value">${checkOut}</div>
                    </div>
                    <div class="info-row">
                        <div class="info-label">Số khách:</div>
                        <div class="info-value">${guests} người</div>
                    </div>
                </div>

                <div class="section">
                    <div class="section-title">🏨 Thông tin phòng</div>
                    <div class="info-row">
                        <div class="info-label">Loại phòng:</div>
                        <div class="info-value">Deluxe 2 giường (Deluxe Twin)</div>
                    </div>
                    <div class="info-row">
                        <div class="info-label">Khách sạn:</div>
                        <div class="info-value">Citadines Marina Ha Long</div>
                    </div>
                    <div class="info-row">
                        <div class="info-label">Địa chỉ:</div>
                        <div class="info-value">Hạ Long, Quảng Ninh, Việt Nam</div>
                    </div>
                </div>

                <div class="section">
                    <div class="section-title">💰 Chi tiết thanh toán</div>
                    <div class="summary">
                        <div class="info-row">
                            <div class="info-label">Giá phòng/đêm 123:</div>
                            <div class="info-value">${BookingPayment.getRoomPricePerNight(bookingInfo || {}).toLocaleString('vi-VN')} VND</div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Số đêm:</div>
                            <div class="info-value">${BookingPayment.calculateNights(checkIn, checkOut)} đêm</div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Tạm tính:</div>
                            <div class="info-value">${(BookingPayment.calculateNights(checkIn, checkOut) * BookingPayment.getRoomPricePerNight(bookingInfo || {})).toLocaleString('vi-VN')} VND</div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Thuế (10%):</div>
                            <div class="info-value">${Math.round(BookingPayment.calculateNights(checkIn, checkOut) * BookingPayment.getRoomPricePerNight(bookingInfo || {}) * 0.1).toLocaleString('vi-VN')} VND</div>
                        </div>
                        <div class="total">
                            Tổng tiền: ${BookingPayment.calculateTotalAmount(checkIn, checkOut, bookingInfo)} VND
                        </div>
                    </div>
                </div>
            </div>

            <div class="footer">
                <p>Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi!</p>
                <p>📞 Hotline: 1900 1234 | 📧 Email: support@hotelbooking.com</p>
                <p>Ngày tạo: ${new Date().toLocaleDateString('vi-VN')}</p>
            </div>
        </body>
        </html>
    `;

    // Tạo blob và tải file
    const blob = new Blob([invoiceHtml], { type: 'text/html' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Hoa-don-${bookingId}.html`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
