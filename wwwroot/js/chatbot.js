// Chatbot functionality
$(document).ready(function () {
    const $chatbotWidget = $('#chatbot-widget');
    const $chatbotButton = $('#chatbot-button');
    const $chatbotNudge = $('#chatbot-nudge');
    const $chatbotPanel = $('#chatbot-panel');
    const $chatbotClose = $('#chatbot-close');
    const $chatbotMaximize = $('#chatbot-maximize');
    const $chatbotMessages = $('#chatbot-messages');
    const $chatbotInput = $('#chatbot-input-field');
    const $chatbotSend = $('#chatbot-send');

    let isOpen = false;
    let isFullscreen = false;

    // *** SỬA LỖI 1: Thêm biến để lưu "bộ nhớ" (context) của chatbot ***
    let currentChatContext = "";

    // *** TÍNH NĂNG MỚI: Chat History Management ***
    let chatHistory = JSON.parse(localStorage.getItem('chatbot_history') || '[]');
    let currentChatId = null;

    // *** TÍNH NĂNG MỚI: Clear Chat Function ***
    function clearCurrentChat() {
        $chatbotMessages.empty();
        currentChatContext = "";
        currentChatId = null;
        console.log("Chat đã được xóa");
    }

    // *** TÍNH NĂNG MỚI: Create New Chat ***
    function createNewChat() {
        // Lưu chat hiện tại nếu có nội dung
        if ($chatbotMessages.children().length > 0) {
            saveCurrentChat();
        }

        // Tạo chat mới
        clearCurrentChat();
        currentChatId = 'chat_' + Date.now();
        addMessage("Chào bạn! Mình có thể giúp gì cho bạn hôm nay?", true);
    }

    // *** TÍNH NĂNG MỚI: Save Current Chat ***
    function saveCurrentChat() {
        if (currentChatId && $chatbotMessages.children().length > 0) {
            const chatData = {
                id: currentChatId,
                title: getChatTitle(),
                messages: $chatbotMessages.html(),
                context: currentChatContext,
                timestamp: new Date().toISOString()
            };

            // Tìm và cập nhật hoặc thêm mới
            const existingIndex = chatHistory.findIndex(chat => chat.id === currentChatId);
            if (existingIndex >= 0) {
                chatHistory[existingIndex] = chatData;
            } else {
                chatHistory.unshift(chatData);
            }

            // Giới hạn 20 chat gần nhất
            if (chatHistory.length > 20) {
                chatHistory = chatHistory.slice(0, 20);
            }

            localStorage.setItem('chatbot_history', JSON.stringify(chatHistory));
            updateChatHistory();
        }
    }

    // *** TÍNH NĂNG MỚI: Get Chat Title ***
    function getChatTitle() {
        const firstUserMessage = $chatbotMessages.find('.user-message .message-content').first().text();
        if (firstUserMessage) {
            return firstUserMessage.length > 30 ? firstUserMessage.substring(0, 30) + '...' : firstUserMessage;
        }
        return 'Chat mới';
    }

    // *** TÍNH NĂNG MỚI: Load Chat from History ***
    function loadChatFromHistory(chatId) {
        const chat = chatHistory.find(c => c.id === chatId);
        if (chat) {
            clearCurrentChat();
            currentChatId = chatId;
            currentChatContext = chat.context || "";
            $chatbotMessages.html(chat.messages);
            scrollToBottom();
            console.log("Đã tải chat:", chat.title);
        }
    }

    // *** TÍNH NĂNG MỚI: Update Chat History UI ***
    function updateChatHistory() {
        const $historyList = $('#chat-history-list');
        if ($historyList.length === 0) return;

        $historyList.empty();

        chatHistory.forEach(chat => {
            const isActive = chat.id === currentChatId;
            const chatItem = $(`
                <div class="chat-history-item ${isActive ? 'active' : ''}" data-chat-id="${chat.id}">
                    <div class="chat-title">${chat.title}</div>
                    <div class="chat-time">${new Date(chat.timestamp).toLocaleDateString('vi-VN')}</div>
                    <button class="delete-chat-btn" data-chat-id="${chat.id}">×</button>
                </div>
            `);
            $historyList.append(chatItem);
        });
    }

    // Toggle chatbot panel
    function toggleChatbot() {
        isOpen = !isOpen;
        if (isOpen) {
            $chatbotPanel.addClass('show');
            $chatbotButton.addClass('active');
            $chatbotInput.focus();
            stopNudge();
            updateChatHistory(); // Cập nhật lịch sử khi mở
        } else {
            $chatbotPanel.removeClass('show fullscreen');
            $chatbotButton.removeClass('active');
            isFullscreen = false;
            updateMaximizeIcon();
            startNudgeCycle();
        }
    }

    // Toggle fullscreen mode
    function toggleFullscreen() {
        isFullscreen = !isFullscreen;
        if (isFullscreen) {
            $chatbotPanel.addClass('fullscreen');
        } else {
            $chatbotPanel.removeClass('fullscreen');
        }
        updateMaximizeIcon();
    }

    // Update maximize icon based on current state
    function updateMaximizeIcon() {
        const icon = isFullscreen ? 'bi-arrows-angle-contract' : 'bi-arrows-fullscreen';
        $chatbotMaximize.find('i').removeClass().addClass('bi ' + icon);
    }

    // Add message to chat
    function addMessage(content, isBot = false) {
        const messageClass = isBot ? 'bot-message' : 'user-message';
        const messageHtml = `<div class="message ${messageClass}"><div class="message-content">${content}</div></div>`;
        $chatbotMessages.append(messageHtml);
        
        // Execute any scripts in the appended content - IMPORTANT: Scripts must be executed after append
        const messageElement = $chatbotMessages.children().last();
        const scripts = messageElement.find('script');
        scripts.each(function() {
            const scriptElement = this;
            const script = document.createElement('script');
            
            // Copy all attributes
            Array.from(scriptElement.attributes).forEach(attr => {
                script.setAttribute(attr.name, attr.value);
            });
            
            // Copy content
            script.text = scriptElement.text || scriptElement.innerHTML;
            
            // Remove old script
            scriptElement.parentNode.removeChild(scriptElement);
            
            // Append new script to head to execute it
            document.head.appendChild(script);
        });
        
        scrollToBottom();
    }

    // Scroll to bottom of messages
    function scrollToBottom() {
        $chatbotMessages.scrollTop($chatbotMessages[0].scrollHeight);
    }

    // Send message to AI
    function sendMessage() {
        const message = $chatbotInput.val().trim();
        if (!message) return;

        // Tạo chat ID nếu chưa có
        if (!currentChatId) {
            currentChatId = 'chat_' + Date.now();
        }

        // Add user message
        addMessage(message, false);
        $chatbotInput.val('');

        // Show typing indicator
        const typingHtml = `<div class="message bot-message"><div class="message-content typing"><span></span><span></span><span></span></div></div>`;
        $chatbotMessages.append(typingHtml);
        scrollToBottom();

        // Get current page context
        const currentUrl = window.location.href;

        // *** SỬA LỖI 2: Xóa bỏ logic lấy context từ URL ***
        // Logic này không cần thiết vì C# đã xử lý 'currentUrl'
        // và logic này đã ghi đè lên 'currentChatContext' gây ra lỗi.
        /*
        let context = '';
        if (currentPath.includes('/Public/Search') || currentPath.includes('/Public/Hotel')) {
            // ... (CODE CŨ GÂY LỖI - ĐÃ XÓA) ...
        }
        */

        // Send to AI API
        $.ajax({
            url: '/Chat/SendMessage',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                message: message,
                // *** SỬA LỖI 3: Gửi "bộ nhớ" đã lưu ***
                context: currentChatContext,
                currentUrl: currentUrl
            }),
            success: function (response) {
                // Remove typing indicator
                $chatbotMessages.find('.typing').parent().remove();

                if (response.success) {
                    addMessage(response.reply, true);

                    // *** SỬA LỖI 4: Lưu "bộ nhớ" mới từ C# trả về ***
                    if (response.newContext) {
                        currentChatContext = response.newContext;
                        console.log("Chat Context MỚI ĐÃ LƯU:", currentChatContext);
                    }

                    // *** TÍNH NĂNG MỚI: Tự động lưu chat sau mỗi tin nhắn ***
                    saveCurrentChat();
                } else {
                    addMessage(response.reply || 'Xin lỗi, tôi gặp sự cố. Vui lòng thử lại sau.', true);
                }
            },
            error: function () {
                // Remove typing indicator
                $chatbotMessages.find('.typing').parent().remove();
                addMessage('Xin lỗi, tôi gặp sự cố. Vui lòng thử lại sau.', true);
            }
        });
    }

    // Event handlers
    $chatbotButton.click(toggleChatbot);
    $chatbotClose.click(toggleChatbot);
    $chatbotMaximize.click(toggleFullscreen);
    $chatbotSend.click(sendMessage);

    $chatbotInput.keypress(function (e) {
        if (e.which === 13) { // Enter key
            sendMessage();
        }
    });

    // *** TÍNH NĂNG MỚI: Event handlers cho chat history ***
    $(document).on('click', '.chat-history-item', function () {
        const chatId = $(this).data('chat-id');
        loadChatFromHistory(chatId);
    });

    $(document).on('click', '.delete-chat-btn', function (e) {
        e.stopPropagation();
        const chatId = $(this).data('chat-id');
        if (confirm('Bạn có chắc muốn xóa cuộc trò chuyện này?')) {
            chatHistory = chatHistory.filter(chat => chat.id !== chatId);
            localStorage.setItem('chatbot_history', JSON.stringify(chatHistory));
            updateChatHistory();

            // Nếu đang xem chat bị xóa, tạo chat mới
            if (currentChatId === chatId) {
                createNewChat();
            }
        }
    });

    // *** TÍNH NĂNG MỚI: Event handler cho nút X (clear chat) ***
    $(document).on('click', '#chatbot-clear', function () {
        if (confirm('Bạn có chắc muốn xóa cuộc trò chuyện hiện tại?')) {
            clearCurrentChat();
            addMessage("Chào bạn! Mình có thể giúp gì cho bạn hôm nay?", true);
        }
    });

    // *** TÍNH NĂNG MỚI: Event handler cho nút tạo chat mới ***
    $(document).on('click', '#chatbot-new-chat', function () {
        createNewChat();
    });

    // Close on outside click
    $(document).click(function (e) {
        if (isOpen && !$chatbotWidget.is(e.target) && $chatbotWidget.has(e.target).length === 0) {
            toggleChatbot();
        }
    });

    // ===== Nudge/Shake logic when chat closed =====
    let nudgeTimer = null;
    function doNudgeOnce() {
        if (isOpen) return;
        $chatbotButton.addClass('nudge');
        $chatbotNudge.addClass('show');
        setTimeout(() => {
            $chatbotButton.removeClass('nudge');
            $chatbotNudge.removeClass('show');
        }, 2000);
    }

    function startNudgeCycle() {
        stopNudge();
        nudgeTimer = setInterval(doNudgeOnce, 15000);
    }

    function stopNudge() {
        if (nudgeTimer) { clearInterval(nudgeTimer); nudgeTimer = null; }
        $chatbotButton.removeClass('nudge');
        $chatbotNudge.removeClass('show');
    }

    // kick off nudge after page load
    startNudgeCycle();

    // Function to open image modal with gallery
    window.openImageModal = function (imageUrl, roomName, allImages = []) {
        // Create modal HTML with navigation and gallery
        var modalHtml = `
            <div id="imageModal" style="
                position: fixed; 
                top: 0; 
                left: 0; 
                width: 100%; 
                height: 100%; 
                background: rgba(0,0,0,0.3); 
                z-index: 10000; 
                display: flex; 
                align-items: center; 
                justify-content: center;
                cursor: pointer;
            " onclick="closeImageModal()">
                <div id="imageModalContainer" style="
                    max-width: 80%; 
                    max-height: 85%; 
                    position: relative;
                    background: rgba(255,255,255,0.95);
                    border-radius: 0px !important;
                    overflow: hidden;
                    box-shadow: 0 10px 30px rgba(0,0,0,0.2);
                    backdrop-filter: blur(10px);
                " onclick="event.stopPropagation()">
                    <!-- Close button -->
                    <div style="
                        position: absolute; 
                        top: 10px; 
                        right: 10px; 
                        background: rgba(0,0,0,0.7); 
                        color: white; 
                        border: none; 
                        border-radius: 50%; 
                        width: 35px; 
                        height: 35px; 
                        cursor: pointer;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        font-size: 20px;
                        z-index: 10001;
                    " onclick="closeImageModal()">×</div>
                    
                    <!-- Navigation arrows -->
                    <div style="
                        position: absolute; 
                        top: 50%; 
                        left: 10px; 
                        transform: translateY(-50%);
                        background: rgba(0,0,0,0.5); 
                        color: white; 
                        border: none; 
                        border-radius: 50%; 
                        width: 40px; 
                        height: 40px; 
                        cursor: pointer;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        font-size: 18px;
                        z-index: 10001;
                    " onclick="navigateImage(-1)">‹</div>
                    
                    <div style="
                        position: absolute; 
                        top: 50%; 
                        right: 10px; 
                        transform: translateY(-50%);
                        background: rgba(0,0,0,0.5); 
                        color: white; 
                        border: none; 
                        border-radius: 50%; 
                        width: 40px; 
                        height: 40px; 
                        cursor: pointer;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        font-size: 18px;
                        z-index: 10001;
                    " onclick="navigateImage(1)">›</div>
                    
                    <!-- Main image -->
                    <div style="position: relative;">
                        <img id="modalMainImage" src="${imageUrl}" alt="${roomName}" style="
                            width: 100%; 
                            height: auto; 
                            max-height: 60vh; 
                            object-fit: contain;
                        " onerror="this.src='/images/default-room.jpg'">
                    </div>
                    
                    
                    <!-- Thumbnail gallery -->
                    <div id="thumbnailGallery" style="
                        padding: 15px; 
                        background: transparent !important; 
                        display: flex; 
                        gap: 8px; 
                        overflow-x: auto;
                        max-height: 120px;
                    ">
                        ${allImages.map((img, index) => `
                            <img src="${img}" alt="Thumbnail ${index + 1}" style="
                                width: 80px; 
                                height: 80px; 
                                object-fit: cover; 
                                border-radius: 0px !important; 
                                cursor: pointer;
                                border: 2px solid ${img === imageUrl ? '#3b82f6' : '#e5e7eb'};
                                flex-shrink: 0;
                            " onclick="selectImage('${img}')" onerror="this.src='/images/default-room.jpg'">
                        `).join('')}
                    </div>
                </div>
            </div>
        `;

        // Add modal to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Store current image index and all images
        window.currentImageIndex = allImages.indexOf(imageUrl);
        window.allImages = allImages;

        // Prevent body scroll
        document.body.style.overflow = 'hidden';

        // Force override CSS
        setTimeout(function () {
            var modal = document.getElementById('imageModalContainer');
            if (modal) {
                modal.style.borderRadius = '0px !important';
            }
            var gallery = document.getElementById('thumbnailGallery');
            if (gallery) {
                gallery.style.background = 'transparent !important';
            }
            var thumbnails = document.querySelectorAll('#thumbnailGallery img');
            thumbnails.forEach(function (img) {
                img.style.borderRadius = '0px !important';
            });
        }, 100);
    };

    // Function to close image modal
    window.closeImageModal = function () {
        var modal = document.getElementById('imageModal');
        if (modal) {
            modal.remove();
            document.body.style.overflow = 'auto';
        }
    };

    // Function to navigate images
    window.navigateImage = function (direction) {
        if (!window.allImages || window.allImages.length <= 1) return;

        window.currentImageIndex += direction;

        // Wrap around
        if (window.currentImageIndex < 0) {
            window.currentImageIndex = window.allImages.length - 1;
        } else if (window.currentImageIndex >= window.allImages.length) {
            window.currentImageIndex = 0;
        }

        var newImageUrl = window.allImages[window.currentImageIndex];
        var mainImage = document.getElementById('modalMainImage');
        if (mainImage) {
            mainImage.src = newImageUrl;
        }

        // Update thumbnail borders
        updateThumbnailBorders(newImageUrl);
    };

    // Function to select image from thumbnail
    window.selectImage = function (imageUrl) {
        var mainImage = document.getElementById('modalMainImage');
        if (mainImage) {
            mainImage.src = imageUrl;
        }

        // Update current index
        window.currentImageIndex = window.allImages.indexOf(imageUrl);

        // Update thumbnail borders
        updateThumbnailBorders(imageUrl);
    };

    // Function to update thumbnail borders
    function updateThumbnailBorders(activeImageUrl) {
        var thumbnails = document.querySelectorAll('#thumbnailGallery img');
        thumbnails.forEach(function (thumb) {
            if (thumb.src === activeImageUrl || thumb.getAttribute('src') === activeImageUrl) {
                thumb.style.border = '2px solid #3b82f6';
            } else {
                thumb.style.border = '2px solid #e5e7eb';
            }
        });
    }

    // Close modal on Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeImageModal();
        }
    });

    // Function to show booking form in chat - defined here to ensure it's always available
    window.showBookingFormInChat = function(propertyId, roomId, roomPriceId, pricePackageId, roomName, hotelName, roomPriceAmount) {
        console.log('showBookingFormInChat called with:', { propertyId, roomId, roomPriceId, pricePackageId, roomName, hotelName, roomPriceAmount });
        
        try {
            // Kiểm tra xem đã có form chưa, nếu có thì xóa
            const existingForm = document.querySelector('[id^="chatBookingForm_"]');
            if (existingForm) {
                existingForm.remove();
            }
            
            // Load booking form via API
            fetch('/Chat/GetBookingForm', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    propertyId: propertyId,
                    roomId: roomId,
                    propertyName: hotelName,
                    roomName: roomName,
                    checkIn: null,
                    checkOut: null,
                    guests: 2,
                    roomPriceId: roomPriceId,
                    pricePackageId: pricePackageId,
                    roomPriceAmount: roomPriceAmount || 2000000
                })
            })
            .then(response => response.text())
            .then(html => {
                // Tìm chatbot messages container và thêm form vào cuối - wrap trong bot-message để có cùng styling với form 2
                const messagesContainer = document.getElementById('chatbot-messages');
                if (messagesContainer) {
                    // Wrap form trong bot-message structure giống như form 2
                    const messageWrapper = document.createElement('div');
                    messageWrapper.className = 'message bot-message';
                    const messageContent = document.createElement('div');
                    messageContent.className = 'message-content';
                    messageContent.innerHTML = html;
                    messageWrapper.appendChild(messageContent);
                    messagesContainer.appendChild(messageWrapper);
                    
                    // Execute any scripts in the injected HTML
                    const scripts = messageContent.querySelectorAll('script');
                    scripts.forEach(function(oldScript) {
                        const script = document.createElement('script');
                        Array.from(oldScript.attributes).forEach(attr => {
                            script.setAttribute(attr.name, attr.value);
                        });
                        script.text = oldScript.text || oldScript.innerHTML;
                        oldScript.parentNode.removeChild(oldScript);
                        document.head.appendChild(script);
                    });
                    
                    // Set min date cho ngày đi khi ngày đến thay đổi
                    const checkInInput = messageContent.querySelector('[name="bookingCheckIn"]');
                    const checkOutInput = messageContent.querySelector('[name="bookingCheckOut"]');
                    if (checkInInput && checkOutInput) {
                        checkInInput.addEventListener('change', function() {
                            if (this.value) {
                                const nextDay = new Date(this.value);
                                nextDay.setDate(nextDay.getDate() + 1);
                                checkOutInput.min = nextDay.toISOString().split('T')[0];
                                if (checkOutInput.value && checkOutInput.value <= this.value) {
                                    checkOutInput.value = '';
                                }
                            }
                        });
                    }
                    
                    // Scroll xuống cuối
                    messagesContainer.scrollTop = messagesContainer.scrollHeight;
                }
            })
            .catch(error => {
                console.error('Error loading booking form:', error);
                alert('Có lỗi xảy ra khi tải form đặt phòng. Vui lòng thử lại.');
            });
        } catch (error) {
            console.error('Error in showBookingFormInChat:', error);
            alert('Có lỗi xảy ra khi hiển thị form đặt phòng. Vui lòng thử lại.');
        }
    };
});