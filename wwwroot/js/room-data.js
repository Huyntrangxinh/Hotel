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
          <img src="${firstPhoto.preview}" alt="${
        category === "bedroom" ? "Bedroom" : "Bathroom"
      }" />
          <div class="additional-photo-overlay">
            <button type="button" class="btn btn-preview btn-sm" onclick="previewRoomPhoto('${
              firstPhoto.preview
            }')">
              <i class="bi bi-eye me-1"></i>Xem
            </button>
            <button type="button" class="btn btn-delete btn-sm" onclick="deleteRoomPhoto('${
              firstPhoto.preview
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
          <input name="Beds[${idx}].Count" type="number" min="1" value="${
        count || ""
      }" class="form-control form-control-sm" placeholder="Nhập số" data-js-created="true">
        </div>`;
      singleRows.appendChild(row);
    }
    function ensureSingle() {
      if (!singleRows.querySelector(".bed-row") && savedBeds.length === 0)
        addSingleBedRow();
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
          <input name="Bedrooms[${i}].Beds[${j}].Count" type="number" min="1" value="${
        count || ""
      }" class="form-control form-control-sm" placeholder="Nhập số" data-js-created="true">
        </div>
        <div class="col-auto">
          <button type="button" class="btn btn-danger btn-sm js-remove-bed-row" style="background-color: #dc3545 !important; border-color: #dc3545 !important; color: white !important; ${
            j < 1 ? "display:none;" : ""
          }">
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
          <div class="mb-2 fw-semibold"><i class="bi bi-plus-square-dotted me-2"></i>phòng ngủ ${
            index + 1
          }</div>
          <div class="bed-rows"></div>
          <button type="button" class="btn btn-outline-primary btn-sm mt-2 js-add-bed">+ Thêm loại giường khác</button>
        </div>`;
      bedroomsContainer.appendChild(card);
      // KHÔNG tự động tạo bed row mặc định - để loadSavedBeds() xử lý
    }
    btnAddBedroom?.addEventListener("click", () => {
      // Đếm chính xác số phòng ngủ hiện có
      const currentCount =
        bedroomsContainer.querySelectorAll(".bedroom").length;
      createBedroom(currentCount);
      // Tạo 1 bed row mặc định cho bedroom mới
      const newBedroom = bedroomsContainer.querySelector(
        `.bedroom[data-index="${currentCount}"]`
      );
      if (newBedroom) {
        addBedRowToBedroom(newBedroom);
      }
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

      // Xóa bed row
      const removeBedBtn = target.closest(".js-remove-bed-row");
      if (removeBedBtn) {
        e.preventDefault();
        const row = removeBedBtn.closest(".bed-row");
        const bedroom = row.closest(".bedroom");

        // Chỉ cho phép xóa nếu còn nhiều hơn 1 bed row
        const bedRows = bedroom.querySelectorAll(".bed-row");
        if (bedRows.length > 1) {
          row.remove();
          reindexBedRows(bedroom);
        }
        return;
      }
    });

    // Function để reindex các loại giường trong một phòng ngủ
    function reindexBedRows(bedroomCard) {
      const bedRows = bedroomCard.querySelectorAll(".bed-row");
      const bedroomIndex = bedroomCard.dataset.index;

      bedRows.forEach((row, newBedIndex) => {
        // Cập nhật name attribute cho select và input
        const select = row.querySelector('select[name^="Bedrooms["]');
        const input = row.querySelector('input[name^="Bedrooms["]');
        const removeBtn = row.querySelector(".js-remove-bed-row");

        if (select) {
          select.name = `Bedrooms[${bedroomIndex}].Beds[${newBedIndex}].Type`;
        }
        if (input) {
          input.name = `Bedrooms[${bedroomIndex}].Beds[${newBedIndex}].Count`;
        }

        // Ẩn/hiện nút xóa: chỉ hiện khi có nhiều hơn 1 bed row
        if (removeBtn) {
          if (bedRows.length > 1) {
            removeBtn.style.display = "block";
          } else {
            removeBtn.style.display = "none";
          }
        }
      });
    }

    function showSingle() {
      singleBox.classList.remove("d-none");
      multiBox.classList.add("d-none");
      setDisabled(singleBox, false);
      setDisabled(multiBox, true);
      ensureSingle();
      // Ẩn nút "Thêm loại giường" khi chọn phòng ngủ đơn
      btnAddSingleBed.style.display = "none";
    }
    function showMulti() {
      singleBox.classList.add("d-none");
      multiBox.classList.remove("d-none");
      setDisabled(singleBox, true);
      setDisabled(multiBox, false);
      // Hiện lại nút "Thêm loại giường" khi chọn nhiều phòng ngủ
      btnAddSingleBed.style.display = "block";
      // Chỉ tạo 2 phòng ngủ mặc định nếu chưa có và chưa có saved data
      if (
        !bedroomsContainer.querySelector(".bedroom") &&
        savedBeds.length === 0
      ) {
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

      // Kiểm tra xem đã có server-rendered fields chưa
      const existingSingleRows = singleRows.querySelectorAll(".bed-row");
      const existingBedrooms = bedroomsContainer.querySelectorAll(".bedroom");

      if (existingSingleRows.length > 0 || existingBedrooms.length > 0) {
        console.log("🚫 Server-rendered fields detected - skipping load");
        return;
      }

      // Clear form
      singleRows.innerHTML = "";
      bedroomsContainer.innerHTML = "";

      const isSingle = document.querySelector(
        'input[name="IsSingleBedroom"][value="true"]'
      ).checked;

      if (isSingle) {
        // Single bedroom: load từ savedBeds
        const savedBeds = window.savedBeds || [];
        console.log("Single bedroom - savedBeds:", savedBeds);

        if (savedBeds.length > 0) {
          // Có data: render dựa trên data
          savedBeds.forEach((bed, index) => {
            console.log(`Loading bed ${index}:`, bed);
            addSingleBedRow(bed.Type, bed.Count);
          });
        } else {
          // Không có data: tạo 1 row mặc định
          console.log("No saved beds - creating default row");
          addSingleBedRow();
        }
      } else {
        // Multi bedroom: load từ savedBedrooms
        const savedBedrooms = window.savedBedrooms || [];
        console.log("Multi bedroom - savedBedrooms:", savedBedrooms);

        if (savedBedrooms.length > 0) {
          // Có data: render dựa trên data
          savedBedrooms.forEach((bedroom, bedroomIndex) => {
            console.log(
              `Creating bedroom ${bedroomIndex} with beds:`,
              bedroom.Beds
            );

            // Create bedroom
            createBedroom(bedroomIndex);
            const bedroomElement = bedroomsContainer.querySelector(
              `.bedroom[data-index="${bedroomIndex}"]`
            );

            // Add beds to bedroom
            if (bedroom.Beds && bedroom.Beds.length > 0) {
              bedroom.Beds.forEach((bed) => {
                addBedRowToBedroom(bedroomElement, bed.Type, bed.Count);
              });
            } else {
              // Nếu bedroom không có beds, tạo 1 bed row mặc định
              addBedRowToBedroom(bedroomElement);
            }
          });
        } else {
          // Không có data: tạo 2 phòng ngủ mặc định
          console.log("No saved bedrooms - creating default 2 bedrooms");
          createBedroom(0);
          createBedroom(1);
        }
      }
    }

    // Load saved data first
    loadSavedBeds();

    // Then setup UI based on loaded data
    const startSingle = document.querySelector(
      'input[name="IsSingleBedroom"][value="true"]'
    ).checked;
    startSingle ? showSingle() : showMulti();

    // Force style cho nút xóa phòng ngủ
    function forceDeleteButtonStyle() {
      const deleteButtons = document.querySelectorAll(".js-remove-bedroom");
      deleteButtons.forEach((btn) => {
        btn.style.backgroundColor = "#dc3545";
        btn.style.borderColor = "#dc3545";
        btn.style.color = "white";
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
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initSecurityDeposit);
  } else {
    initSecurityDeposit();
  }

  // ===== XỬ LÝ CHỌN TẤT CẢ TIỆN NGHI =====
  function initSelectAllAmenities() {
    const root = document.getElementById("amenitiesRoot");
    const selectAllCheckbox = document.getElementById("amenitiesMaster");
    if (!root || !selectAllCheckbox) return;
    let isBulkUpdating = false;

    const getBoxes = () => root.querySelectorAll("input.amenity-checkbox");

    // Cập nhật trạng thái nút "Chọn tất cả" dựa trên các checkbox tiện nghi
    function updateSelectAllState() {
      const boxes = getBoxes();
      const checkedCount = root.querySelectorAll(
        "input.amenity-checkbox:checked"
      ).length;
      const totalCount = boxes.length;

      console.log(
        `UpdateSelectAllState: ${checkedCount}/${totalCount} checked`
      );

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
      getBoxes().forEach((cb) => {
        if (cb.checked !== checked) {
          cb.checked = checked;
          cb.dispatchEvent(new Event("change", { bubbles: true }));
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
    root.addEventListener("change", (e) => {
      if (!(e.target instanceof Element)) return;
      if (isBulkUpdating) return;
      if (e.target.matches("input.amenity-checkbox")) updateSelectAllState();
    });

    // Cho phép click cả vào vùng tiêu đề để toggle
    document
      .getElementById("amenitiesMasterContainer")
      ?.addEventListener("click", (e) => {
        if (!(e.target instanceof Element)) return;
        if (e.target.id === "amenitiesMaster") return; // đã xử lý ở trên
        selectAllCheckbox.checked = !selectAllCheckbox.checked;
        setAllAmenities(selectAllCheckbox.checked);
      });

    // Khởi tạo trạng thái ban đầu sau khi DOM sẵn sàng và có selected từ server
    updateSelectAllState();
    console.log("Select all amenities initialized successfully");
  }

  // Khởi tạo khi DOM ready
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initSelectAllAmenities);
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

    // ===== FORM SUBMIT HANDLER =====
    form.addEventListener("submit", (e) => {
      // TẠM THỜI CHẶN SUBMIT ĐỂ DEBUG
      e.preventDefault();

      console.log("=== BẮT ĐẦU FORM SUBMIT ===");

      // Kiểm tra IsSingleBedroom
      const isSingleBedroom = document.querySelector(
        'input[name="IsSingleBedroom"][value="true"]'
      ).checked;
      console.log("IsSingleBedroom:", isSingleBedroom);

      // Log dữ liệu bedrooms trước khi xử lý
      if (isSingleBedroom) {
        console.log("=== SINGLE BEDROOM DATA (TRƯỚC) ===");
        const bedRows = document.querySelectorAll("#singleBeds .bed-row");
        bedRows.forEach((row, index) => {
          const typeSelect = row.querySelector('select[name^="Beds["]');
          const countInput = row.querySelector('input[name^="Beds["]');
          console.log(
            `Bed ${index}: Type="${typeSelect?.value}", Count="${countInput?.value}"`
          );
        });
      } else {
        console.log("=== MULTI BEDROOM DATA (TRƯỚC) ===");
        const bedrooms = document.querySelectorAll(
          "#bedroomsContainer .bedroom"
        );
        bedrooms.forEach((bedroom, bedroomIndex) => {
          console.log(`Bedroom ${bedroomIndex}:`);
          const bedRows = bedroom.querySelectorAll(".bed-row");
          bedRows.forEach((row, bedIndex) => {
            const typeSelect = row.querySelector('select[name^="Bedrooms["]');
            const countInput = row.querySelector('input[name^="Bedrooms["]');
            console.log(
              `  Bed ${bedIndex}: Type="${typeSelect?.value}", Count="${countInput?.value}"`
            );
          });
        });
      }

      // Xóa hidden inputs trước khi submit
      const hiddenCountInputs = document.querySelectorAll(
        'input[type="hidden"][name*="Count"]'
      );
      hiddenCountInputs.forEach((input) => {
        input.remove();
      });

      // Cập nhật DeletedPhotoIds field
      updateDeletedPhotoIdsField();

      // Copy files từ roomPhotos array vào input files
      const bedroomInput = document.getElementById("bedroomPhotos");
      const bathroomInput = document.getElementById("bathroomPhotos");
      const additionalInput = document.getElementById("additionalPhotos");

      const bedroomPhotos = roomPhotos.filter(
        (p) => p.category === "bedroom" && p.file
      );
      const bathroomPhotos = roomPhotos.filter(
        (p) => p.category === "bathroom" && p.file
      );
      const additionalPhotos = roomPhotos.filter(
        (p) => p.category === "additional" && p.file
      );

      // Tạo DataTransfer để copy files vào input
      if (bedroomPhotos.length > 0 && bedroomInput) {
        const dt = new DataTransfer();
        bedroomPhotos.forEach((photo) => dt.items.add(photo.file));
        bedroomInput.files = dt.files;
      }

      if (bathroomPhotos.length > 0 && bathroomInput) {
        const dt = new DataTransfer();
        bathroomPhotos.forEach((photo) => dt.items.add(photo.file));
        bathroomInput.files = dt.files;
      }

      if (additionalPhotos.length > 0 && additionalInput) {
        const dt = new DataTransfer();
        additionalPhotos.forEach((photo) => dt.items.add(photo.file));
        additionalInput.files = dt.files;
      }

      // Log dữ liệu bedrooms sau khi xử lý
      if (isSingleBedroom) {
        console.log("=== SINGLE BEDROOM DATA (SAU) ===");
        const bedRows = document.querySelectorAll("#singleBeds .bed-row");
        bedRows.forEach((row, index) => {
          const typeSelect = row.querySelector('select[name^="Beds["]');
          const countInput = row.querySelector('input[name^="Beds["]');
          console.log(
            `Bed ${index}: Type="${typeSelect?.value}", Count="${countInput?.value}"`
          );
        });
      } else {
        console.log("=== MULTI BEDROOM DATA (SAU) ===");
        const bedrooms = document.querySelectorAll(
          "#bedroomsContainer .bedroom"
        );
        bedrooms.forEach((bedroom, bedroomIndex) => {
          console.log(`Bedroom ${bedroomIndex}:`);
          const bedRows = bedroom.querySelectorAll(".bed-row");
          bedRows.forEach((row, bedIndex) => {
            const typeSelect = row.querySelector('select[name^="Bedrooms["]');
            const countInput = row.querySelector('input[name^="Bedrooms["]');
            console.log(
              `  Bed ${bedIndex}: Type="${typeSelect?.value}", Count="${countInput?.value}"`
            );
          });
        });
      }

      console.log("=== KẾT THÚC FORM SUBMIT ===");

      // Để submit thực sự, bỏ comment dòng dưới:
      form.submit();
    });
  })();
})();
