(() => {
  const OFFSET = 20; // giảm offset theo layout mới
  const links = [...document.querySelectorAll(".wizard-steps a")];
  let isAutoScrolling = false;

  const highlightTimers = {};
  function setActive(id) {
    links.forEach((l) =>
      l.parentElement.classList.toggle(
        "active",
        l.getAttribute("href") === "#" + id
      )
    );
    // highlight section bên phải chỉ trong 3s
    ids.forEach((secId) => {
      const el = document.getElementById(secId);
      if (!el) return;
      el.classList.remove("card-highlight");
      if (secId === id) {
        el.classList.add("card-highlight");
        if (highlightTimers[secId]) clearTimeout(highlightTimers[secId]);
        highlightTimers[secId] = setTimeout(() => {
          el.classList.remove("card-highlight");
        }, 1000);
      }
    });
  }
  function smoothTo(href) {
    const el = document.querySelector(href);
    if (!el) return;
    const top = el.getBoundingClientRect().top + window.scrollY - OFFSET;
    isAutoScrolling = true;
    window.scrollTo({ top, behavior: "smooth" });
    const id = href.replace("#", "");
    setTimeout(() => {
      isAutoScrolling = false;
      setActive(id);
    }, 600);
  }
  // Click: set active ngay + scroll có bù offset
  links.forEach((a) =>
    a.addEventListener("click", (e) => {
      e.preventDefault();
      const id = a.getAttribute("href").replace("#", "");
      setActive(id);
      smoothTo("#" + id);
    })
  );
  // Scroll: chọn section hiện hành
  const ids = [
    "sec-info",
    "sec-detail",
    "sec-bed",
    "sec-amenities",
    "sec-capacity",
    "sec-security",
    "sec-photos",
  ];
  const onScroll = () => {
    if (isAutoScrolling) return;
    let best = ids[0];
    let bestTop = Number.POSITIVE_INFINITY;
    ids.forEach((id) => {
      const el = document.getElementById(id);
      if (!el) return;
      const r = el.getBoundingClientRect();
      const t = Math.abs(r.top);
      if (r.top <= window.innerHeight * 0.5 && t < bestTop) {
        bestTop = t;
        best = id;
      }
    });
    setActive(best);
  };
  document.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("load", onScroll);

  // ====== XỬ LÝ TẢI ẢNH ======
  let roomPhotos = []; // Mảng lưu tất cả ảnh phòng
  let deletedPhotoIds = []; // Mảng lưu ID ảnh đã xóa

  // Load ảnh đã lưu từ server (khi edit phòng)
  const savedPhotoUrls = window.savedPhotoUrls || [];
  const savedPhotoCategories = window.savedPhotoCategories || [];
  const savedPhotoData = window.savedPhotoData || [];
  console.log("=== DEBUG: LOADING SAVED PHOTOS ===");
  console.log("Saved photos from server:", savedPhotoUrls);
  console.log("Saved photo categories:", savedPhotoCategories);
  console.log("Saved photo data:", savedPhotoData);

  // Sử dụng savedPhotoData nếu có, fallback về cách cũ
  if (savedPhotoData && savedPhotoData.length > 0) {
    console.log("Using savedPhotoData (new method)");
    savedPhotoData.forEach((photoData, index) => {
      const photo = {
        id: "saved_" + index,
        file: null, // Không có file mới
        preview: photoData.Url,
        category: photoData.Category,
        isNew: false,
        dbId: photoData.Id, // Sử dụng ID thực từ database
      };
      roomPhotos.push(photo);
      console.log("Added saved photo (new method):", photo);
    });
  } else if (savedPhotoUrls && savedPhotoUrls.length > 0) {
    console.log("Using savedPhotoUrls (fallback method)");
    savedPhotoUrls.forEach((url, index) => {
      // Lấy category từ database, mặc định là 'additional' nếu không có
      const category = savedPhotoCategories[index] || "additional";

      // Tạo photo object với ảnh đã lưu
      const photo = {
        id: "saved_" + index,
        file: null, // Không có file mới
        preview: url,
        category: category,
        isNew: false,
        dbId: index + 1, // ID tạm để track
      };
      roomPhotos.push(photo);
      console.log("Added saved photo (fallback method):", photo);
    });
  }

  // Event listeners cho các input file
  document.getElementById("bedroomPhotos")?.addEventListener("change", (e) => {
    handleRoomPhotoUpload(e, "bedroom");
  });

  document.getElementById("bathroomPhotos")?.addEventListener("change", (e) => {
    handleRoomPhotoUpload(e, "bathroom");
  });

  document
    .getElementById("additionalPhotos")
    ?.addEventListener("change", (e) => {
      handleRoomPhotoUpload(e, "additional");
    });

  document
    .getElementById("moreAdditionalPhotos")
    ?.addEventListener("change", (e) => {
      handleRoomPhotoUpload(e, "additional");
    });

  // Click vào upload zone để mở file picker (chỉ khi không có ảnh)
  document.getElementById("bedroomUpload")?.addEventListener("click", (e) => {
    // Chỉ mở file picker nếu không có ảnh
    const photos = roomPhotos.filter((p) => p.category === "bedroom");
    if (photos.length === 0) {
      document.getElementById("bedroomPhotos").click();
    }
  });

  document.getElementById("bathroomUpload")?.addEventListener("click", (e) => {
    // Chỉ mở file picker nếu không có ảnh
    const photos = roomPhotos.filter((p) => p.category === "bathroom");
    if (photos.length === 0) {
      document.getElementById("bathroomPhotos").click();
    }
  });

  document
    .getElementById("additionalUploadLarge")
    ?.addEventListener("click", () => {
      document.getElementById("additionalPhotos").click();
    });

  // Nút "Tải lên thêm ảnh" trong lưới thumbnail
  document.getElementById("btnAddMorePhotos")?.addEventListener("click", () => {
    document.getElementById("moreAdditionalPhotos").click();
  });

  function handleRoomPhotoUpload(e, category) {
    const files = [...e.target.files];
    if (files.length > 0) {
      files.forEach((file) => {
        const photo = {
          id: Date.now(),
          file: file,
          preview: URL.createObjectURL(file),
          category: category,
          isNew: true,
        };
        roomPhotos.push(photo);
      });

      // Cập nhật UI ngay lập tức
      updateRoomPhotoUI();

      // KHÔNG RESET INPUT - ĐỂ FILES CÒN TRONG FORM
      // e.target.value = ""; // ← COMMENT LẠI

      console.log("Photos uploaded:", roomPhotos); // Debug log
      console.log("Input files after upload:", e.target.files); // Debug log
    }
  }

  function updateRoomPhotoUI() {
    // Cập nhật khu vực Bedroom
    updateCategoryZone("bedroom");

    // Cập nhật khu vực Bathroom
    updateCategoryZone("bathroom");

    // Cập nhật khu vực ảnh bổ sung
    updateAdditionalPhotosUI();
  }

  function updateCategoryZone(category) {
    const uploadZone = document.getElementById(category + "Upload");
    const uploadContent = uploadZone.querySelector(".upload-content");
    const photos = roomPhotos.filter((p) => p.category === category);

    console.log(`Updating ${category} zone with photos:`, photos); // Debug log

    if (photos.length > 0) {
      // Có ảnh: hiển thị ảnh với style giống additional photos
      const firstPhoto = photos[0];
      uploadContent.innerHTML = `
        <div class="additional-photo-item" data-photo-id="${firstPhoto.id}">
          <img src="${firstPhoto.preview}" alt="${category === "bedroom" ? "Bedroom" : "Bathroom"
        }" />
          <div class="additional-photo-overlay">
            <button type="button" class="btn btn-preview btn-sm" onclick="previewRoomPhoto('${firstPhoto.preview
        }')">
              <i class="bi bi-eye me-1"></i>Xem
            </button>
            <button type="button" class="btn btn-delete btn-sm" onclick="deleteRoomPhoto('${firstPhoto.preview
        }')">
              <i class="bi bi-trash me-1"></i>Xóa
            </button>
          </div>
        </div>
      `;
    } else {
      // Không có ảnh: hiển thị giao diện tải
      uploadContent.innerHTML = `
        <i class="bi bi-camera fs-1 text-muted mb-2"></i>
        <h6 class="mb-2">${category === "bedroom" ? "Bedroom" : "Bathroom"}</h6>
        <a href="#" class="btn btn-link p-0 text-primary">Tải lên ảnh</a>
      `;
    }
  }

  // Function riêng để xử lý tải thêm ảnh (tránh vòng lặp)
  function handleAddMorePhotos(category) {
    const inputId = category + "Photos";
    const input = document.getElementById(inputId);
    if (input) {
      input.click();
    }
  }

  function updateAdditionalPhotosUI() {
    const largeUpload = document.getElementById("additionalUploadLarge");
    const thumbnailGrid = document.getElementById("additionalPhotosGrid");
    const additionalPhotos = roomPhotos.filter(
      (p) => p.category === "additional"
    );

    if (additionalPhotos.length === 0) {
      // Chưa có ảnh: hiển thị khu vực tải lớn
      largeUpload.classList.remove("d-none");
      thumbnailGrid.classList.add("d-none");
    } else {
      // Đã có ảnh: hiển thị lưới thumbnail
      largeUpload.classList.add("d-none");
      thumbnailGrid.classList.remove("d-none");

      // Render lưới thumbnail
      renderAdditionalPhotosGrid();
    }
  }

  function renderAdditionalPhotosGrid() {
    const container = document.getElementById("additionalPhotosContainer");
    container.innerHTML = "";

    const additionalPhotos = roomPhotos.filter(
      (p) => p.category === "additional"
    );
    additionalPhotos.forEach((photo, index) => {
      const col = document.createElement("div");
      col.className = "col-6 col-md-4 col-lg-3";
      col.innerHTML = `
        <div class="additional-photo-item" data-photo-id="${photo.id}">
          <img src="${photo.preview}" alt="Ảnh bổ sung" />
          <div class="additional-photo-overlay">
            <button type="button" class="btn btn-preview btn-sm" onclick="previewRoomPhoto('${photo.preview}')">
              <i class="bi bi-eye me-1"></i>Xem
            </button>
            <button type="button" class="btn btn-delete btn-sm" onclick="deleteRoomPhoto('${photo.preview}')">
              <i class="bi bi-trash me-1"></i>Xóa
            </button>
          </div>
        </div>
      `;
      container.appendChild(col);
    });
  }

  function previewRoomPhoto(imageUrl) {
    // Tạo modal preview trực tiếp từ URL
    const modal = document.createElement("div");
    modal.className =
      "position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center";
    modal.style.cssText = "background: rgba(0,0,0,0.8); z-index: 9999;";
    modal.innerHTML = `
      <div class="position-relative">
        <img src="${imageUrl}" class="img-fluid" style="max-height: 80vh; max-width: 80vw;" />
        <button type="button" class="btn-close position-absolute top-0 end-0 m-2" onclick="this.closest('.position-fixed').remove()"></button>
      </div>
    `;
    document.body.appendChild(modal);

    // Click ngoài để đóng
    modal.addEventListener("click", (e) => {
      if (e.target === modal) modal.remove();
    });
  }

  function deleteRoomPhoto(imageUrl) {
    console.log("=== DEBUG: DELETE PHOTO CLICKED ===");
    console.log("Image URL to delete:", imageUrl);
    console.log("Current roomPhotos array:", roomPhotos);

    // Tìm và xóa ảnh theo URL
    const photo = roomPhotos.find((p) => p.preview === imageUrl);
    console.log("Found photo to delete:", photo);

    if (photo) {
      // Nếu là ảnh đã lưu (có ID thực), thêm vào danh sách xóa
      if (!photo.isNew && photo.dbId) {
        deletedPhotoIds.push(photo.dbId);
      }

      // Giải phóng URL object
      URL.revokeObjectURL(photo.preview);

      // Xóa khỏi mảng
      const index = roomPhotos.findIndex((p) => p.preview === imageUrl);
      roomPhotos.splice(index, 1);

      // Cập nhật UI
      updateRoomPhotoUI();
    }
  }

  // Export ra global để onclick="..." hoạt động
  window.deleteRoomPhoto = deleteRoomPhoto;
  window.previewRoomPhoto = previewRoomPhoto;

  // Khởi tạo UI
  console.log("Initializing UI with photos:", roomPhotos); // Debug log
  updateRoomPhotoUI();

  // ====== CÀI ĐẶT GIƯỜNG NGỦ ======
  (function bedSetup() {
    const bedTypes = window.bedTypes || [];
    const savedBeds = window.savedBeds || [];
    const singleBox = document.getElementById("singleBedroomBox");
    const multiBox = document.getElementById("multiBedroomBox");
    const singleRows = document.getElementById("singleBeds");
    const btnAddSingleBed = document.getElementById("btnAddSingleBed");
    const bedroomsContainer = document.getElementById("bedroomsContainer");
    const btnAddBedroom = document.getElementById("btnAddBedroom");

    console.log("=== BED SETUP DEBUG ===");
    console.log("bedTypes:", bedTypes);
    console.log("savedBeds:", savedBeds);

    function setDisabled(container, disabled) {
      container
        ?.querySelectorAll("input, select, button")
        .forEach((el) => (el.disabled = disabled));
    }

    function addSingleBedRow(type = bedTypes[0], count = "") {
      const idx = singleRows.querySelectorAll(".bed-row").length;
      const row = document.createElement("div");
      row.className = "row g-3 align-items-center bed-row mb-2";
      row.innerHTML = `
        <div class="col-md-6">
          <label class="form-label mb-1">Loại giường</label>
          <select name="Beds[${idx}].Type" class="form-select form-select-sm">
            ${bedTypes
          .map(
            (b) => `<option ${b === type ? "selected" : ""}>${b}</option>`
          )
          .join("")}
          </select>
        </div>
        <div class="col-md-6">
          <label class="form-label mb-1">Số lượng giường</label>
          <input name="Beds[${idx}].Count" type="number" min="1" value="${count || ''}" class="form-control form-control-sm" placeholder="Nhập số">
        </div>`;
      singleRows.appendChild(row);
    }
    function ensureSingle() {
      if (!singleRows.querySelector(".bed-row") && savedBeds.length === 0) addSingleBedRow();
    }
    btnAddSingleBed?.addEventListener("click", () => addSingleBedRow());

    function addBedRowToBedroom(card, type = bedTypes[0], count = "") {
      const bedRows = card.querySelector(".bed-rows");
      const i = +card.dataset.index;
      const j = bedRows.querySelectorAll(".bed-row").length;
      const row = document.createElement("div");
      row.className = "row g-3 align-items-center bed-row mb-2";

      // Luôn render nút xóa, điều khiển hiển thị bằng CSS sau khi reindex
      row.innerHTML = `
        <div class="col-md-5">
          <label class="form-label mb-1">Loại giường</label>
          <select name="Bedrooms[${i}].Beds[${j}].Type" class="form-select form-select-sm">
            ${bedTypes
          .map(
            (b) => `<option ${b === type ? "selected" : ""}>${b}</option>`
          )
          .join("")}
          </select>
          <input type="hidden" name="Bedrooms[${i}].Beds[${j}].BedroomIndex" value="${i}">
        </div>
        <div class="col-md-5">
          <label class="form-label mb-1">Số lượng giường</label>
          <input name="Bedrooms[${i}].Beds[${j}].Count" type="number" min="1" value="${count || ''}" class="form-control form-control-sm" placeholder="Nhập số">
        </div>
        <div class="col-auto">
          <button type="button" class="btn btn-danger btn-sm js-remove-bed-row" style="background-color: #dc3545 !important; border-color: #dc3545 !important; color: white !important; ${j < 1 ? 'display:none;' : ''}">
            <i class="bi bi-trash3-fill"></i>
          </button>
        </div>`;
      bedRows.appendChild(row);
      // Đảm bảo cập nhật hiển thị và name sau khi thêm
      reindexBedRows(card);
    }
    function createBedroom(index) {
      const card = document.createElement("div");
      card.className = "bed-box rounded-3 p-3 mb-3 bedroom";
      card.dataset.index = index;

      card.innerHTML = `
        <div class="position-relative">
          <div class="mb-2 fw-semibold"><i class="bi bi-plus-square-dotted me-2"></i>${index + 1
        } phòng ngủ</div>
          <div class="bed-rows"></div>
          <button type="button" class="btn btn-outline-primary btn-sm mt-2 js-add-bed">+ Thêm loại giường khác</button>
        </div>`;
      bedroomsContainer.appendChild(card);
      addBedRowToBedroom(card);
    }
    btnAddBedroom?.addEventListener("click", () => {
      // Đếm chính xác số phòng ngủ hiện có
      const currentCount =
        bedroomsContainer.querySelectorAll(".bedroom").length;
      createBedroom(currentCount);
    });
    // Gắn lắng nghe cấp tài liệu để hỗ trợ cả phần tử render server-side
    document.addEventListener("click", (e) => {
      const target = e.target;

      // Thêm loại giường khác
      const addBedBtn = target.closest(".js-add-bed");
      if (addBedBtn) {
        e.preventDefault();
        const card = addBedBtn.closest(".bedroom");
        addBedRowToBedroom(card);
        return;
      }

      // Không hỗ trợ xóa giường/phòng ngủ nữa
    });

    // Bỏ toàn bộ handler xóa

    // Function để reindex các loại giường trong một phòng ngủ
    function reindexBedRows(bedroomCard) {
      const bedRows = bedroomCard.querySelectorAll(".bed-row");
      const bedroomIndex = bedroomCard.dataset.index;

      bedRows.forEach((row, newBedIndex) => {
        // Cập nhật name attribute cho select và input
        const select = row.querySelector('select[name^="Bedrooms["]');
        const input = row.querySelector('input[name^="Bedrooms["]');

        if (select) {
          select.name = `Bedrooms[${bedroomIndex}].Beds[${newBedIndex}].Type`;
        }
        if (input) {
          input.name = `Bedrooms[${bedroomIndex}].Beds[${newBedIndex}].Count`;
        }

        // Ẩn/hiện nút xóa dựa trên index mới (từ loại giường thứ 2 trở đi)
        // Không còn render nút xóa
      });
    }

    function showSingle() {
      singleBox.classList.remove("d-none");
      multiBox.classList.add("d-none");
      setDisabled(singleBox, false);
      setDisabled(multiBox, true);
      ensureSingle();
      // Ẩn nút "Thêm loại giường" khi chọn phòng ngủ đơn
      btnAddSingleBed.style.display = 'none';
    }
    function showMulti() {
      singleBox.classList.add("d-none");
      multiBox.classList.remove("d-none");
      setDisabled(singleBox, true);
      setDisabled(multiBox, false);
      // Hiện lại nút "Thêm loại giường" khi chọn nhiều phòng ngủ
      btnAddSingleBed.style.display = 'block';
      // Chỉ tạo 2 phòng ngủ mặc định nếu chưa có và chưa có saved data
      if (!bedroomsContainer.querySelector(".bedroom") && savedBeds.length === 0) {
        createBedroom(0);
        createBedroom(1);
      }
    }
    document.querySelectorAll('input[name="IsSingleBedroom"]').forEach((r) =>
      r.addEventListener("change", () => {
        const isSingle = document.querySelector(
          'input[name="IsSingleBedroom"][value="true"]'
        ).checked;
        isSingle ? showSingle() : showMulti();
      })
    );

    // Load saved bed data
    function loadSavedBeds() {
      console.log("=== LOADING SAVED BEDS ===");
      console.log("savedBeds:", savedBeds);
      console.log("savedBeds length:", savedBeds.length);
      console.log("savedBeds details:", JSON.stringify(savedBeds, null, 2));

      // Clear existing rows
      singleRows.innerHTML = '';
      bedroomsContainer.innerHTML = '';

      // Check if single or multi bedroom mode
      const isSingle = document.querySelector('input[name="IsSingleBedroom"][value="true"]').checked;

      if (savedBeds.length === 0) {
        console.log("No saved beds found - creating default form");
        // Tạo form mặc định khi chưa có data
        if (isSingle) {
          addSingleBedRow(); // Tạo 1 row mặc định
        } else {
          createBedroom(0); // Tạo 2 phòng ngủ mặc định
          createBedroom(1);
        }
        return;
      }

      if (isSingle) {
        // Single bedroom mode - load all beds into single section
        savedBeds.forEach((bed, index) => {
          console.log(`Loading bed ${index} (single):`, bed);
          addSingleBedRow(bed.Type, bed.Count);
        });
      } else {
        // Multi bedroom mode - group beds by BedroomIndex
        const bedsByBedroom = {};
        savedBeds.forEach((bed, index) => {
          console.log(`Loading bed ${index} (multi):`, bed);
          const bedroomIndex = bed.BedroomIndex || 0;
          if (!bedsByBedroom[bedroomIndex]) {
            bedsByBedroom[bedroomIndex] = [];
          }
          bedsByBedroom[bedroomIndex].push(bed);
        });

        // Create bedrooms and add beds
        Object.keys(bedsByBedroom).sort((a, b) => parseInt(a) - parseInt(b)).forEach((bedroomIndex, arrayIndex) => {
          const bedroomBeds = bedsByBedroom[bedroomIndex];
          console.log(`Creating bedroom ${bedroomIndex} with beds:`, bedroomBeds);

          // Create bedroom with sequential index (0, 1, 2...)
          createBedroom(arrayIndex);
          const bedroom = bedroomsContainer.querySelector(`.bedroom[data-index="${arrayIndex}"]`);

          // Add beds to bedroom
          bedroomBeds.forEach(bed => {
            addBedRowToBedroom(bedroom, bed.Type, bed.Count);
          });
        });

        // Cập nhật hiển thị nút xóa cho các phòng ngủ đã load
        [...bedroomsContainer.querySelectorAll(".bedroom")].forEach((bedroom, newIndex) => {
          const deleteBtn = bedroom.querySelector(".js-remove-bedroom");
          if (deleteBtn) {
            if (newIndex >= 2) {
              deleteBtn.style.display = "block";
            } else {
              deleteBtn.style.display = "none";
            }
          }
          // Reindex các loại giường trong phòng ngủ này
          reindexBedRows(bedroom);
        });
      }
    }

    const startSingle = document.querySelector(
      'input[name="IsSingleBedroom"][value="true"]'
    ).checked;
    startSingle ? showSingle() : showMulti();

    // Load saved data after UI is set up
    loadSavedBeds();

    // Force style cho nút xóa phòng ngủ
    function forceDeleteButtonStyle() {
      const deleteButtons = document.querySelectorAll('.js-remove-bedroom');
      deleteButtons.forEach(btn => {
        btn.style.backgroundColor = '#dc3545';
        btn.style.borderColor = '#dc3545';
        btn.style.color = 'white';
      });
    }

    // Chạy ngay lập tức và sau khi load
    forceDeleteButtonStyle();
    setTimeout(forceDeleteButtonStyle, 100);
    setTimeout(forceDeleteButtonStyle, 500);
  })();

  // ===== Format SecurityDeposit with dot separators =====
  function initSecurityDeposit() {
    const hidden = document.getElementById("SecurityDeposit");
    const display = document.getElementById("SecurityDepositDisplay");
    if (!hidden || !display) return;

    // init from model value: LẤY PHẦN NGUYÊN trước dấu . hoặc , rồi mới giữ chữ số
    const rawInit = (hidden.value || "").toString();
    const integerPart = rawInit.split(/[.,]/)[0];
    const initDigits = integerPart.replace(/[^0-9]/g, "");
    if (initDigits) {
      hidden.value = initDigits;
      display.value = numberToDots(initDigits);
    } else {
      display.value = "";
    }

    display.addEventListener("input", () => {
      // Chỉ lấy số nguyên, loại bỏ dấu chấm và ký tự khác
      const digits = display.value.replace(/[^0-9]/g, "");
      hidden.value = digits; // store pure number
      display.value = numberToDots(digits);
    });

    function numberToDots(nStr) {
      if (!nStr) return "";
      // Chỉ format phần nguyên, không format số thập phân
      return nStr.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    }
  }

  // Bảo đảm init sau khi DOM sẵn sàng
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initSecurityDeposit);
  } else {
    initSecurityDeposit();
  }

  // ===== XỬ LÝ CHỌN TẤT CẢ TIỆN NGHI =====
  function initSelectAllAmenities() {
    const root = document.getElementById('amenitiesRoot');
    const selectAllCheckbox = document.getElementById('amenitiesMaster');
    if (!root || !selectAllCheckbox) return;
    let isBulkUpdating = false;

    const getBoxes = () => root.querySelectorAll('input.amenity-checkbox');

    // Cập nhật trạng thái nút "Chọn tất cả" dựa trên các checkbox tiện nghi
    function updateSelectAllState() {
      const boxes = getBoxes();
      const checkedCount = root.querySelectorAll('input.amenity-checkbox:checked').length;
      const totalCount = boxes.length;

      console.log(`UpdateSelectAllState: ${checkedCount}/${totalCount} checked`);

      if (!selectAllCheckbox) return;
      if (checkedCount === 0) {
        // Không có tiện nghi nào được chọn
        selectAllCheckbox.indeterminate = false;
        selectAllCheckbox.checked = false;
        console.log("→ Select all: unchecked (0 selected)");
      } else if (checkedCount === totalCount) {
        // Tất cả tiện nghi đều được chọn
        selectAllCheckbox.indeterminate = false;
        selectAllCheckbox.checked = true;
        console.log("→ Select all: checked (all selected)");
      } else {
        // Một phần tiện nghi được chọn (không phải tất cả)
        selectAllCheckbox.indeterminate = true;
        selectAllCheckbox.checked = false;
        console.log("→ Select all: indeterminate (partial selection)");
      }
    }

    function setAllAmenities(checked) {
      isBulkUpdating = true;
      getBoxes().forEach(cb => {
        if (cb.checked !== checked) {
          cb.checked = checked;
          cb.dispatchEvent(new Event('change', { bubbles: true }));
        }
      });
      isBulkUpdating = false;
      updateSelectAllState();
    }

    // Xử lý khi click nút "Chọn tất cả"
    selectAllCheckbox?.addEventListener("change", function () {
      const isChecked = this.checked;
      console.log("Select all clicked:", isChecked);
      setAllAmenities(isChecked);
      console.log("All amenities updated to:", isChecked);
    });

    // Xử lý khi click từng checkbox tiện nghi (event delegation)
    root.addEventListener('change', (e) => {
      if (!(e.target instanceof Element)) return;
      if (isBulkUpdating) return;
      if (e.target.matches('input.amenity-checkbox')) updateSelectAllState();
    });

    // Cho phép click cả vào vùng tiêu đề để toggle
    document.getElementById('amenitiesMasterContainer')?.addEventListener('click', (e) => {
      if (!(e.target instanceof Element)) return;
      if (e.target.id === 'amenitiesMaster') return; // đã xử lý ở trên
      selectAllCheckbox.checked = !selectAllCheckbox.checked;
      setAllAmenities(selectAllCheckbox.checked);
    });

    // Khởi tạo trạng thái ban đầu sau khi DOM sẵn sàng và có selected từ server
    updateSelectAllState();
    console.log("Select all amenities initialized successfully");
  }

  // Khởi tạo khi DOM ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initSelectAllAmenities);
  } else {
    initSelectAllAmenities();
  }

  // ===== Client validation for photos & price =====
  (function requiredPhotosAndPrice() {
    const form = document.getElementById("roomForm");
    if (!form) return;
    const bedroomUpload = document.getElementById("bedroomUpload");
    const bathroomUpload = document.getElementById("bathroomUpload");
    const bedErr = document.getElementById("bedroomError");
    const bathErr = document.getElementById("bathroomError");
    const hiddenPrice = document.getElementById("SecurityDeposit");

    function setZoneError(zone, errEl, hasError) {
      if (hasError) {
        zone.style.borderColor = "#dc3545";
        zone.style.borderStyle = "dashed";
        errEl?.classList.remove("d-none");
      } else {
        zone.style.borderColor = "#dee2e6";
        zone.style.borderStyle = "dashed";
        errEl?.classList.add("d-none");
      }
    }

    function hasPhoto(zone) {
      return zone.querySelector(".additional-photo-item img") != null; // when previewed
    }

    // Function để cập nhật hidden field DeletedPhotoIds
    function updateDeletedPhotoIdsField() {
      const deletedPhotoIdsInput = document.getElementById("DeletedPhotoIds");
      if (deletedPhotoIdsInput) {
        deletedPhotoIdsInput.value = JSON.stringify(deletedPhotoIds);
        console.log("=== CẬP NHẬT HIDDEN FIELD ===");
        console.log("DeletedPhotoIds field value:", deletedPhotoIdsInput.value);
      }
    }

    // Export function để có thể sử dụng từ bên ngoài
    window.updateDeletedPhotoIdsField = updateDeletedPhotoIdsField;

    // ===== TẠM THỜI CHẶN SUBMIT ĐỂ DEBUG =====
    form.addEventListener("submit", (e) => {
      // Cập nhật DeletedPhotoIds field
      updateDeletedPhotoIdsField();

      // ===== LOG CHI TIẾT ĐỂ DEBUG =====
      console.log("=== DEBUG FORM TRƯỚC KHI SUBMIT ===");
      console.log("DeletedPhotoIds:", deletedPhotoIds);
      console.log("Total photos in roomPhotos array:", roomPhotos.length);

      // ===== KIỂM TRA INPUT FILES =====
      console.log("=== KIỂM TRA INPUT FILES ===");

      const bedroomInput = document.getElementById("bedroomPhotos");
      const bathroomInput = document.getElementById("bathroomPhotos");
      const additionalInput = document.getElementById("additionalPhotos");
      const moreAdditionalInput = document.getElementById(
        "moreAdditionalPhotos"
      );

      console.log("BedroomPhotos input:", bedroomInput);
      console.log("BathroomPhotos input:", bathroomInput);
      console.log("AdditionalPhotos input:", additionalInput);
      console.log("MoreAdditionalPhotos input:", moreAdditionalInput);

      // ===== KIỂM TRA FILES TRONG INPUT =====
      if (bedroomInput) {
        console.log("BedroomPhotos files:", bedroomInput.files);
        console.log(
          "BedroomPhotos files.length:",
          bedroomInput.files?.length || 0
        );
        console.log("BedroomPhotos files[0]:", bedroomInput.files?.[0]);
      }

      if (bathroomInput) {
        console.log("BathroomPhotos files:", bathroomInput.files);
        console.log(
          "BathroomPhotos files.length:",
          bathroomInput.files?.length || 0
        );
        console.log("BathroomPhotos files[0]:", bathroomInput.files?.[0]);
      }

      if (additionalInput) {
        console.log("AdditionalPhotos files:", additionalInput.files);
        console.log(
          "AdditionalPhotos files.length:",
          additionalInput.files?.length || 0
        );
        console.log("AdditionalPhotos files[0]:", additionalInput.files?.[0]);
      }

      if (moreAdditionalInput) {
        console.log("MoreAdditionalPhotos files:", moreAdditionalInput.files);
        console.log(
          "MoreAdditionalPhotos files.length:",
          moreAdditionalInput.files?.length || 0
        );
        console.log(
          "MoreAdditionalPhotos files[0]:",
          moreAdditionalInput.files?.[0]
        );
      }

      // ===== COPY FILES TỪ ROOMPHOTOS VÀO INPUT TRƯỚC KHI SUBMIT =====
      console.log("=== COPY FILES VÀO INPUT ===");

      // Copy files từ roomPhotos array vào input files
      const bedroomPhotos = roomPhotos.filter(
        (p) => p.category === "bedroom" && p.file
      );
      const bathroomPhotos = roomPhotos.filter(
        (p) => p.category === "bathroom" && p.file
      );
      const additionalPhotos = roomPhotos.filter(
        (p) => p.category === "additional" && p.file
      );

      console.log("Files từ roomPhotos array:");
      console.log("- Bedroom photos:", bedroomPhotos.length);
      console.log("- Bathroom photos:", bathroomPhotos.length);
      console.log("- Additional photos:", additionalPhotos.length);

      // Tạo DataTransfer để copy files vào input
      if (bedroomPhotos.length > 0 && bedroomInput) {
        const dt = new DataTransfer();
        bedroomPhotos.forEach((photo) => dt.items.add(photo.file));
        bedroomInput.files = dt.files;
        console.log(
          "✅ Đã copy",
          dt.files.length,
          "files vào BedroomPhotos input"
        );
      }

      if (bathroomPhotos.length > 0 && bathroomInput) {
        const dt = new DataTransfer();
        bathroomPhotos.forEach((photo) => dt.items.add(photo.file));
        bathroomInput.files = dt.files;
        console.log(
          "✅ Đã copy",
          dt.files.length,
          "files vào BathroomPhotos input"
        );
      }

      if (additionalPhotos.length > 0 && additionalInput) {
        const dt = new DataTransfer();
        additionalPhotos.forEach((photo) => dt.items.add(photo.file));
        additionalInput.files = dt.files;
        console.log(
          "✅ Đã copy",
          dt.files.length,
          "files vào AdditionalPhotos input"
        );
      }

      // ===== KIỂM TRA FORM DATA SAU KHI COPY =====
      console.log("=== KIỂM TRA FORM DATA SAU KHI COPY ===");
      const formData = new FormData(form);
      console.log("FormData entries:");
      for (let [key, value] of formData.entries()) {
        if (value instanceof File) {
          console.log(
            `  ${key}: File - ${value.name}, ${value.size} bytes, ${value.type}`
          );
        } else {
          console.log(`  ${key}: ${value}`);
        }
      }

      console.log("✅ HOÀN THÀNH XỬ LÝ - FORM SẼ SUBMIT");
      console.log("=== KẾT THÚC DEBUG - FORM SUBMIT ===");

      // Cho phép form submit bình thường
      // Không cần làm gì thêm - form sẽ submit tự động
    });
  })();
})();
