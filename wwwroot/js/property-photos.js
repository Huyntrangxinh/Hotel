/**
 * Property Photos Manager
 * Handles photo upload, preview, category management, and form submission
 */
(function () {
    'use strict';

    // State
    let photos = []; // [{id?, url?, file?, preview, category}]
    let currentPreviewIndex = 0;
    let deletedUrls = [];
    let LOCAL_KEY = '';

    // TEST FLAG: Set to false to block form submission (for testing)
    const BLOCK_SUBMIT = true; // Set to false to allow submission

    // DOM Elements (will be initialized)
    let fileInput, mainTile, grid, mainImg, catHidden, modalEl, modalImg, modalThumbs, dimsLabel;

    // Constants
    const VN_CAT = {
        exterior: 'Ngoại thất / Tòa nhà',
        others: 'Others',
        lobby: 'Lobby',
        swimming_pool: 'Swimming Pool',
        entertainment: 'Entertainment Facility',
        sport: 'Sport Facility',
        hygiene: 'Hygiene Facility',
        bedroom: 'Bedroom',
        bathroom: 'Bathroom',
        functional_hall: 'Functional Hall'
    };

    const catOptionsHtml = Object.entries(VN_CAT)
        .filter(([k]) => k !== 'exterior')
        .map(([val, label]) => `<option value="${val}">${label}</option>`)
        .join('');

    // ========== Helper Functions ==========

    function setMainEmptyUI(isEmpty) {
        const overlay = mainTile.querySelector('.tile-overlay');
        const placeholder = mainTile.querySelector('.upload-placeholder');
        const moreZone = grid.querySelector('.upload-drop-zone');

        if (isEmpty) {
            mainTile.classList.add('empty');
            mainTile.classList.remove('has-photo');
            mainImg.classList.add('d-none');
            overlay.classList.add('d-none');
            placeholder.classList.remove('d-none');
            if (moreZone) moreZone.classList.add('d-none');
        } else {
            mainTile.classList.remove('empty');
            mainTile.classList.add('has-photo');
            mainImg.classList.remove('d-none');
            overlay.classList.remove('d-none');
            placeholder.classList.add('d-none');
            if (moreZone) moreZone.classList.remove('d-none');
        }
    }

    function validateImage(file) {
        return new Promise((resolve, reject) => {
            const maxSizeKB = 300;
            const fileSizeKB = file.size / 1024;

            if (fileSizeKB > maxSizeKB) {
                reject(`Kích cỡ ảnh phải tối đa ${maxSizeKB} KB. Ảnh hiện tại: ${fileSizeKB.toFixed(1)} KB`);
                return;
            }

            if (!file.type.startsWith('image/')) {
                reject('File phải là ảnh');
                return;
            }

            const img = new Image();
            img.onload = function () {
                const width = this.naturalWidth;
                const height = this.naturalHeight;

                if (width < 800 || height < 600) {
                    reject(`Độ phân giải ảnh phải tối thiểu 800×600 px. Ảnh hiện tại: ${width}×${height} px`);
                    return;
                }

                resolve(true);
            };

            img.onerror = function () {
                reject('Không thể đọc ảnh');
            };

            img.src = URL.createObjectURL(file);
        });
    }

    function showError(message) {
        const existingAlerts = document.querySelectorAll('.alert-danger');
        existingAlerts.forEach(alert => alert.remove());

        const alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-danger alert-dismissible fade show';
        alertDiv.innerHTML = `
            <i class="bi bi-exclamation-triangle me-2"></i>
            <strong>Ảnh không hợp lệ:</strong> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        const photoCards = document.querySelectorAll('.card');
        let photoCard = null;

        photoCards.forEach(card => {
            if (card.querySelector('.bi-camera')) {
                photoCard = card;
            }
        });

        if (photoCard && photoCard.nextElementSibling) {
            photoCard.parentNode.insertBefore(alertDiv, photoCard.nextElementSibling);
        } else if (photoCard) {
            photoCard.insertAdjacentElement('afterend', alertDiv);
        } else {
            const mainContent = document.querySelector('.container-fluid .row .col-lg-8');
            if (mainContent) {
                mainContent.insertBefore(alertDiv, mainContent.firstElementChild);
            }
        }

        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }

    // ========== Optimized Functions ==========

    /**
     * Lightweight: Only updates manifest JSON (can be called frequently)
     */
    function updateManifest() {
        let manifest = [];
        let newIndex = 0;

        photos.forEach((p, order) => {
            if (p.file && p.file instanceof File) {
                manifest.push({ newIndex: newIndex++, category: p.category, order: order });
            } else if (p.preview && !p.file) {
                manifest.push({ url: p.preview, category: p.category, order: order });
            }
        });

        const manifestEl = document.getElementById('photoManifest');
        const deletedEl = document.getElementById('deletedPhotoUrls');
        if (manifestEl) manifestEl.value = JSON.stringify(manifest);
        if (deletedEl) deletedEl.value = JSON.stringify(deletedUrls);
        if (catHidden) catHidden.value = JSON.stringify(photos.map(p => p.category));

        // Cache to localStorage
        try {
            const cache = photos.map(p => ({ preview: p.preview, category: p.category }));
            localStorage.setItem(LOCAL_KEY, JSON.stringify(cache));
        } catch { /* ignore quota errors */ }
    }

    /**
     * Heavy: Creates file inputs for form submission (only call before submit)
     */
    function prepareFileInputs() {
        const form = document.querySelector('form');
        if (!form) return;

        // Remove existing file inputs
        [...form.querySelectorAll('input[name^="PropertyPhotos"]')].forEach(i => i.remove());

        const newFiles = photos
            .filter(p => p.file && p.file instanceof File)
            .map(p => p.file);

        if (newFiles.length === 0) return;

        // Create file inputs using DataTransfer
        newFiles.forEach((file, idx) => {
            const inp = document.createElement('input');
            inp.type = 'file';
            inp.name = 'PropertyPhotos';
            inp.style.display = 'none';

            const fileList = new DataTransfer();
            fileList.items.add(file);
            inp.files = fileList.files;

            form.appendChild(inp);
        });
    }

    function renderGrid() {
        // Clear existing thumbnails and upload buttons
        [...grid.querySelectorAll('.tile-wrap, .upload-drop-zone')].forEach(n => n.remove());

        const additionalPhotos = photos.slice(1); // Skip first photo (main)

        // Render photos
        additionalPhotos.forEach((p, idx0) => {
            const wrap = document.createElement('div');
            wrap.className = 'tile-wrap';
            const globalIdx = idx0 + 1;
            wrap.innerHTML = `
                <div class="photo-tile">
                    <img src="${p.preview}" alt="photo ${globalIdx}">
                    <div class="tile-overlay">
                        <a href="#" class="js-preview" data-idx="${globalIdx}">Xem trước</a>
                        <span class="divider"></span>
                        <a href="#" class="js-remove text-danger" data-idx="${globalIdx}">Xóa</a>
                    </div>
                </div>
                <select class="form-select form-select-sm photo-cat" data-idx="${globalIdx}">
                    ${catOptionsHtml}
                </select>
            `;
            grid.appendChild(wrap);
            wrap.querySelector('.photo-cat').value = p.category;
        });

        // Add upload button
        const uploadWrap = document.createElement('div');
        uploadWrap.className = 'tile-wrap';
        const uploadText = window.PropertyPhotos?._uploadMorePhotosText || 'Tải thêm ảnh';
        uploadWrap.innerHTML = `
            <div class="upload-drop-zone grid-item" data-upload-trigger>
                <i class="bi bi-camera mb-2"></i>
                <div class="text">${uploadText}</div>
            </div>
        `;
        grid.appendChild(uploadWrap);

        // Update main photo
        if (photos.length) {
            mainImg.src = photos[0].preview;
            setMainEmptyUI(false);
        } else {
            mainImg.src = '';
            setMainEmptyUI(true);
        }

        // Only update manifest (lightweight), don't create file inputs
        updateManifest();
    }

    function addFiles(fileList) {
        [...fileList].forEach((file) => {
            if (!file.type.startsWith('image/')) {
                showError('File phải là ảnh');
                return;
            }

            validateImage(file)
                .then(() => {
                    const reader = new FileReader();
                    reader.onload = e => {
                        const category = photos.length === 0 ? 'exterior' : 'others';
                        const photoObj = { file, preview: e.target.result, category };
                        photos.push(photoObj);
                        renderGrid();
                    };
                    reader.readAsDataURL(file);
                })
                .catch(error => {
                    showError(error);
                });
        });
    }

    function openPreview(idx) {
        if (!photos[idx]) return;
        setModalImage(idx);
        if (window.bootstrap && window.bootstrap.Modal) {
            new window.bootstrap.Modal(modalEl).show();
        }
    }

    function setModalImage(idx) {
        modalImg.src = photos[idx].preview;
        const im = new Image();
        im.onload = () => {
            if (dimsLabel) dimsLabel.textContent = `${im.naturalWidth} × ${im.naturalHeight}`;
        };
        im.src = photos[idx].preview;
    }

    function loadSavedPhotos(savedCategoriesJson, savedUrlsJson) {
        try {
            let cats = [];
            if (savedCategoriesJson && savedCategoriesJson !== '""' && savedCategoriesJson !== '') {
                try {
                    const parsed = JSON.parse(savedCategoriesJson);
                    cats = typeof parsed === 'string' ? JSON.parse(parsed) : parsed;
                } catch (e) {
                    console.warn('Error parsing categories', e);
                    cats = [];
                }
            }

            const urls = savedUrlsJson ? JSON.parse(savedUrlsJson) : [];

            if (urls.length > 0) {
                photos = urls.map((u, i) => ({
                    file: null,
                    preview: u,
                    category: cats[i] || (i === 0 ? 'exterior' : 'others')
                }));
                renderGrid();
            } else {
                // Try localStorage
                const cached = localStorage.getItem(LOCAL_KEY);
                if (cached) {
                    const arr = JSON.parse(cached);
                    photos = (arr || []).map((x, i) => ({
                        file: null,
                        preview: x.preview,
                        category: x.category || (i === 0 ? 'exterior' : 'others')
                    }));
                    renderGrid();
                }
            }
        } catch (e) {
            console.error('Error loading saved photos', e);
            const cached = localStorage.getItem(LOCAL_KEY);
            if (cached) {
                try {
                    const arr = JSON.parse(cached);
                    photos = (arr || []).map((x, i) => ({
                        file: null,
                        preview: x.preview,
                        category: x.category || (i === 0 ? 'exterior' : 'others')
                    }));
                    renderGrid();
                } catch (e2) {
                    console.error('Error loading from localStorage', e2);
                }
            }
        }
    }

    // ========== Form Submit Handler ==========

    function handleFormSubmit(formEl) {
        console.log('===DEBUG===photos: Setting up submit handler for form:', formEl.action || 'no action');

        formEl.addEventListener('submit', function (e) {
            console.log('===DEBUG===photos: Form submit event triggered!');
            e.preventDefault();

            console.log('===DEBUG===photos');
            console.log('Total photos:', photos.length);

            // Phân loại ảnh cũ và ảnh mới
            const oldPhotos = photos.filter(p => p.preview && !p.file);
            const newPhotos = photos.filter(p => p.file && p.file instanceof File);

            console.log('=== List ảnh cũ (existing photos) ===');
            console.log(`Count: ${oldPhotos.length}`);
            oldPhotos.forEach((p, i) => {
                console.log(`  Old[${i}]:`, {
                    preview: p.preview,
                    category: p.category
                });
            });

            console.log('=== List ảnh mới (new photos) ===');
            console.log(`Count: ${newPhotos.length}`);
            newPhotos.forEach((p, i) => {
                console.log(`  New[${i}]:`, {
                    fileName: p.file?.name,
                    fileSize: p.file?.size,
                    fileType: p.file?.type,
                    preview: p.preview?.substring(0, 50) || 'no preview',
                    category: p.category
                });
            });

            // Capture files BEFORE any manipulation
            const filesToUpload = photos
                .filter(p => p.file && p.file instanceof File)
                .map(p => p.file);

            if (filesToUpload.length === 0 && photos.length === 0) {
                // No photos at all, submit normally
                formEl.submit();
                return;
            }

            // Update manifest (lightweight)
            updateManifest();

            // Prepare file inputs (heavy, only when needed)
            prepareFileInputs();

            // Wait a bit for DOM update
            setTimeout(() => {
                // Create hidden form for submission
                const hiddenForm = document.createElement('form');
                hiddenForm.method = 'POST';
                hiddenForm.action = formEl.action;
                hiddenForm.enctype = 'multipart/form-data'; // Required for file uploads
                hiddenForm.style.display = 'none';

                console.log('===DEBUG===photos: Created hidden form');
                console.log(`  Action: ${hiddenForm.action}`);
                console.log(`  Method: ${hiddenForm.method}`);
                console.log(`  Enctype: ${hiddenForm.enctype}`);

                // Copy all form fields
                const formElements = formEl.querySelectorAll('input:not([type="file"]), select, textarea');
                console.log('===DEBUG===photos: Copying form fields...');
                console.log(`Found ${formElements.length} form elements`);

                // First, handle radio buttons separately to ensure only checked ones are copied
                const radioGroups = new Map();
                formElements.forEach(el => {
                    if (el.type === 'radio') {
                        if (!radioGroups.has(el.name)) {
                            radioGroups.set(el.name, []);
                        }
                        radioGroups.get(el.name).push(el);
                    }
                });

                // Copy checked radio buttons
                radioGroups.forEach((radios, name) => {
                    const checked = radios.find(r => r.checked);
                    console.log(`  Radio group "${name}": ${radios.length} radios, checked: ${checked ? checked.value : 'none'}`);
                    if (checked) {
                        const hiddenInput = document.createElement('input');
                        hiddenInput.type = 'hidden';
                        hiddenInput.name = name;
                        hiddenInput.value = checked.value || '';
                        hiddenForm.appendChild(hiddenInput);
                        if (name === 'StarRating') {
                            console.log(`  ✅ Copied StarRating (radio): ${hiddenInput.value}`);
                        }
                    } else {
                        console.log(`  ⚠️ No checked radio found for "${name}"`);
                    }
                });

                // Copy other form fields (non-radio)
                formElements.forEach(el => {
                    // Skip radio buttons (already handled above)
                    if (el.type === 'radio') {
                        return;
                    }

                    if (el.name && el.value !== null && el.value !== undefined) {
                        // Skip if already added (e.g., from radio button handling)
                        if (hiddenForm.querySelector(`input[name="${el.name}"]`)) {
                            return;
                        }

                        const hiddenInput = document.createElement('input');
                        hiddenInput.type = 'hidden';
                        hiddenInput.name = el.name;
                        hiddenInput.value = el.value;
                        hiddenForm.appendChild(hiddenInput);
                        if (el.name === 'PropertyId' || el.name === '__RequestVerificationToken') {
                            console.log(`  ✅ Copied ${el.name}: ${el.value?.substring(0, 50)}`);
                        }
                    }
                });

                // Verify PropertyId is present
                const propertyIdInput = hiddenForm.querySelector('input[name="PropertyId"]');
                if (!propertyIdInput || !propertyIdInput.value) {
                    console.error('❌ ERROR: PropertyId is missing!');
                    console.error('Original form PropertyId:', formEl.querySelector('input[name="PropertyId"]')?.value);
                }

                // Add file inputs
                filesToUpload.forEach((file) => {
                    const fileInput = document.createElement('input');
                    fileInput.type = 'file';
                    fileInput.name = 'PropertyPhotos';
                    fileInput.style.display = 'none';

                    const fileList = new DataTransfer();
                    fileList.items.add(file);
                    fileInput.files = fileList.files;

                    hiddenForm.appendChild(fileInput);
                });

                // Add anti-forgery token
                const originalToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                if (!originalToken) {
                    console.error('❌ ERROR: CSRF token not found!');
                } else {
                    console.log('✅ CSRF token found:', originalToken.substring(0, 20) + '...');
                }

                // Check if token already exists in hiddenForm (from copying form fields)
                let tokenInput = hiddenForm.querySelector('input[name="__RequestVerificationToken"]');
                if (!tokenInput) {
                    tokenInput = document.createElement('input');
                    tokenInput.type = 'hidden';
                    tokenInput.name = '__RequestVerificationToken';
                    tokenInput.value = originalToken;
                    hiddenForm.appendChild(tokenInput);
                }

                document.body.appendChild(hiddenForm);

                // Final check before submit
                console.log('===DEBUG===photos: Form data summary');
                console.log(`  Action: ${hiddenForm.action}`);
                console.log(`  Method: ${hiddenForm.method}`);
                console.log(`  PropertyId: ${hiddenForm.querySelector('input[name="PropertyId"]')?.value || 'MISSING!'}`);
                console.log(`  CSRF Token: ${hiddenForm.querySelector('input[name="__RequestVerificationToken"]')?.value ? 'Present' : 'MISSING!'}`);
                console.log(`  File inputs: ${hiddenForm.querySelectorAll('input[type="file"]').length}`);
                console.log(`  Total inputs: ${hiddenForm.querySelectorAll('input').length}`);

                // Log all StarRating inputs in hidden form
                const starRatingInputs = hiddenForm.querySelectorAll('input[name="StarRating"]');
                console.log(`  StarRating inputs: ${starRatingInputs.length}`);
                starRatingInputs.forEach((input, idx) => {
                    console.log(`    StarRating[${idx}]: ${input.value}`);
                });

                console.log('✅ Submitting form...');
                hiddenForm.submit();
            }, 100);
        });
    }

    // ========== Public API ==========

    window.PropertyPhotos = {
        _uploadMorePhotosText: 'Tải thêm ảnh', // Default text

        init: function (propertyId, savedCategoriesJson, savedUrlsJson, uploadMorePhotosText) {
            // Set LOCAL_KEY
            LOCAL_KEY = `pd_photos_${propertyId}`;

            // Store upload text for later use
            if (uploadMorePhotosText) {
                window.PropertyPhotos._uploadMorePhotosText = uploadMorePhotosText;
            }

            // Get DOM elements
            fileInput = document.getElementById('photoUpload');
            mainTile = document.getElementById('mainPhotoTile');
            grid = document.getElementById('galleryGrid');
            mainImg = document.getElementById('mainPhotoImg');
            catHidden = document.getElementById('photoCategories');
            modalEl = document.getElementById('photoPreviewModal');
            modalImg = document.getElementById('modalImagePreview');
            modalThumbs = document.getElementById('modalThumbnailsContainer');
            dimsLabel = document.getElementById('imageDimensions');

            if (!fileInput || !mainTile || !grid || !mainImg) {
                console.error('PropertyPhotos: Required DOM elements not found');
                return;
            }

            // Update upload button text if provided
            if (uploadMorePhotosText) {
                const uploadButtons = grid.querySelectorAll('.upload-drop-zone .text');
                uploadButtons.forEach(btn => {
                    if (btn.textContent.includes('Tải thêm ảnh')) {
                        btn.textContent = uploadMorePhotosText;
                    }
                });
            }

            // Initialize UI
            setMainEmptyUI(true);
            renderGrid();
            loadSavedPhotos(savedCategoriesJson, savedUrlsJson);

            // Event: Upload triggers
            const uploadLink = mainTile.querySelector('.upload-link');
            if (uploadLink) {
                uploadLink.addEventListener('click', e => {
                    e.preventDefault();
                    fileInput.click();
                });
            }

            // Event: Delete main photo
            const deleteLink = mainTile.querySelector('.delete-link');
            if (deleteLink) {
                deleteLink.addEventListener('click', e => {
                    e.preventDefault();
                    if (photos.length > 0) {
                        photos.splice(0, 1);
                        if (photos.length > 0) {
                            photos[0].category = 'exterior';
                        }
                        renderGrid();
                    }
                });
            }

            // Event: File input change
            fileInput.addEventListener('change', e => addFiles(e.target.files));

            // Events: Drag & drop
            grid.addEventListener('dragover', e => {
                e.preventDefault();
                const uploadZone = e.target.closest('.upload-drop-zone');
                if (uploadZone) uploadZone.classList.add('dragover');
            });

            grid.addEventListener('dragenter', e => {
                e.preventDefault();
                const uploadZone = e.target.closest('.upload-drop-zone');
                if (uploadZone) uploadZone.classList.add('dragover');
            });

            grid.addEventListener('dragleave', e => {
                e.preventDefault();
                const uploadZone = e.target.closest('.upload-drop-zone');
                if (uploadZone) uploadZone.classList.remove('dragover');
            });

            grid.addEventListener('drop', e => {
                e.preventDefault();
                const uploadZone = e.target.closest('.upload-drop-zone');
                if (uploadZone) {
                    uploadZone.classList.remove('dragover');
                    addFiles(e.dataTransfer.files);
                }
            });

            // Event: Grid actions (preview, remove, upload)
            grid.addEventListener('click', e => {
                const pv = e.target.closest('.js-preview');
                const rm = e.target.closest('.js-remove');
                const uploadBtn = e.target.closest('[data-upload-trigger]');

                if (pv) {
                    e.preventDefault();
                    currentPreviewIndex = +pv.dataset.idx || 0;
                    openPreview(currentPreviewIndex);
                }
                if (rm) {
                    e.preventDefault();
                    const i = +rm.dataset.idx;
                    const removed = photos[i];
                    if (removed && removed.preview && !removed.file) {
                        deletedUrls.push(removed.preview);
                    }
                    photos.splice(i, 1);
                    renderGrid();
                }
                if (uploadBtn) {
                    e.preventDefault();
                    e.stopPropagation();
                    fileInput.click();
                }
            });

            // Event: Category change
            grid.addEventListener('change', e => {
                const sel = e.target.closest('.photo-cat');
                if (sel) {
                    const i = +sel.dataset.idx;
                    photos[i].category = sel.value;
                    updateManifest(); // Only update manifest, not file inputs
                }
            });

            // Event: Main tile preview
            const previewLink = mainTile.querySelector('.preview-link');
            if (previewLink) {
                previewLink.addEventListener('click', e => {
                    e.preventDefault();
                    if (photos.length > 0) {
                        openPreview(0);
                    }
                });
            }

            // Setup form submit handler
            // Tìm form có action chứa "PropertyData" (form chính để submit property data)
            const allForms = document.querySelectorAll('form');
            console.log('===DEBUG===photos: Found forms:', allForms.length);
            let formEl = null;
            for (let f of allForms) {
                const action = f.getAttribute('action') || '';
                console.log('  Form action:', action);
                if (action.includes('PropertyData') || (action === '' && f.querySelector('input[name="PropertyId"]'))) {
                    formEl = f;
                    console.log('  ✅ Selected form for PropertyData');
                    break;
                }
            }
            // Fallback: nếu không tìm thấy, dùng form đầu tiên
            if (!formEl && allForms.length > 0) {
                formEl = allForms[0];
                console.log('  ⚠️ Using first form as fallback');
            }
            if (formEl) {
                console.log('===DEBUG===photos: Attaching submit handler to form');
                handleFormSubmit(formEl);
            } else {
                console.warn('===DEBUG===photos: No form found!');
            }
        }
    };
})();

