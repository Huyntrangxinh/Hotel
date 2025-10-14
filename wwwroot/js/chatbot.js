// Chatbot functionality
$(document).ready(function () {
    const $chatbotWidget = $('#chatbot-widget');
    const $chatbotButton = $('#chatbot-button');
    const $chatbotPanel = $('#chatbot-panel');
    const $chatbotClose = $('#chatbot-close');
    const $chatbotMaximize = $('#chatbot-maximize');
    const $chatbotMessages = $('#chatbot-messages');
    const $chatbotInput = $('#chatbot-input-field');
    const $chatbotSend = $('#chatbot-send');

    let isOpen = false;
    let isFullscreen = false;

    // Toggle chatbot panel
    function toggleChatbot() {
        isOpen = !isOpen;
        if (isOpen) {
            $chatbotPanel.addClass('show');
            $chatbotButton.addClass('active');
            $chatbotInput.focus();
        } else {
            $chatbotPanel.removeClass('show fullscreen');
            $chatbotButton.removeClass('active');
            isFullscreen = false;
            updateMaximizeIcon();
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

        // Add user message
        addMessage(message, false);
        $chatbotInput.val('');

        // Show typing indicator
        const typingHtml = `<div class="message bot-message"><div class="message-content typing"><span></span><span></span><span></span></div></div>`;
        $chatbotMessages.append(typingHtml);
        scrollToBottom();

        // Get current page context
        const currentUrl = window.location.href;
        const currentPath = window.location.pathname;

        // Extract search parameters if on search/hotel pages
        let context = '';
        if (currentPath.includes('/Public/Search') || currentPath.includes('/Public/Hotel')) {
            const urlParams = new URLSearchParams(window.location.search);
            const destination = urlParams.get('destination');
            const checkin = urlParams.get('checkin');
            const checkout = urlParams.get('checkout');
            const adults = urlParams.get('adults');
            const children = urlParams.get('children');

            if (destination) context += `Điểm đến: ${destination}. `;
            if (checkin) context += `Ngày nhận phòng: ${checkin}. `;
            if (checkout) context += `Ngày trả phòng: ${checkout}. `;
            if (adults) context += `Số người lớn: ${adults}. `;
            if (children) context += `Số trẻ em: ${children}. `;
        }

        // Send to AI API
        $.ajax({
            url: '/Chat/SendMessage',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                message: message,
                context: context,
                currentUrl: currentUrl
            }),
            success: function (response) {
                // Remove typing indicator
                $chatbotMessages.find('.typing').parent().remove();

                if (response.success) {
                    addMessage(response.reply, true);
                } else {
                    addMessage('Xin lỗi, tôi gặp sự cố. Vui lòng thử lại sau.', true);
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

    // Close on outside click
    $(document).click(function (e) {
        if (isOpen && !$chatbotWidget.is(e.target) && $chatbotWidget.has(e.target).length === 0) {
            toggleChatbot();
        }
    });
});
