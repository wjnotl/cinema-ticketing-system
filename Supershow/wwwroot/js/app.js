$(document).on("mouseover", "[data-hover]", function () {
	$(this).attr("data-hover", "true")
	$(this).find("[data-hover-container]").attr("data-hover-container", "true")
})
$(document).on("mouseout", "[data-hover]", function () {
	$(this).attr("data-hover", "false")
	$(this).find("[data-hover-container]").attr("data-hover-container", "false")
})
$(document).on("resize", function () {
	$(".overlay").each(function () {
		overlayPadder($(this))
	})
})

$("[data-drag-scroll]").each(function () {
	const $container = $(this)
	const $track = $container.find("[data-scroll-track]")
	const $swiperLeft = $container.find("[data-scroll-swiper]:has([data-scroll-prev])")
	const $swiperRight = $container.find("[data-scroll-swiper]:has([data-scroll-next])")
	const $buttonPrev = $container.find("[data-scroll-prev]")
	const $buttonNext = $container.find("[data-scroll-next]")
	const $items = $track.children()

	let isMoving = false
	let isDragging = false
	let startX = 0
	let currentX = 0
	let translateX = 0
	let lastTranslateX = 0

	function clamp(min, x, max) {
		if (x < min) return min
		if (x > max) return max
		return x
	}

	$track.on("mousedown", function (e) {
		isDragging = true
		startX = e.pageX
		$track.css("transition", "none")
	})

	$(document).on("mousemove", function (e) {
		if (!isDragging) return
		currentX = e.pageX
		let delta = currentX - startX
		translateX = clamp($container.width() - $track[0].scrollWidth, lastTranslateX + delta, 0)
		$track.css("transform", `translateX(${translateX}px)`)
	})

	$(document).on("mouseup", function () {
		if (!isDragging) return
		isDragging = false
		lastTranslateX = translateX
		snapToCenter()
	})

	$buttonPrev.on("click", function () {
		moveToItem(-1)
	})

	$buttonNext.on("click", function () {
		moveToItem(1)
	})

	function snapToCenter() {
		const wrapperWidth = $container.width()
		const minTranslate = $container.width() - $track[0].scrollWidth
		const maxTranslate = 0

		const containerOffset = $container.offset().left
		const containerCenter = containerOffset + wrapperWidth / 2

		let closestDistance = Infinity
		let targetTranslate = translateX

		$items.each(function () {
			const $item = $(this)
			const itemCenter = $item.offset().left + $item.outerWidth() / 2
			const distance = Math.abs(containerCenter - itemCenter)

			if (distance < closestDistance) {
				closestDistance = distance

				// Get item's center relative to track
				const itemOffset = $item.position().left + $item.outerWidth() / 2
				targetTranslate = clamp(minTranslate, wrapperWidth / 2 - itemOffset, maxTranslate)
			}
		})

		// Check if first or last item is in view — snap to edges
		const itemWidth = $items.first().outerWidth()

		if (translateX >= maxTranslate - itemWidth / 2) {
			targetTranslate = maxTranslate // Snap to start
			// remove data-fade-out
			$swiperLeft.attr("data-fade-out", "")
			$swiperRight.removeAttr("data-fade-out")
		} else if (translateX <= minTranslate + itemWidth / 2) {
			targetTranslate = minTranslate // Snap to end
			$swiperLeft.removeAttr("data-fade-out")
			$swiperRight.attr("data-fade-out", "")
		} else {
			$swiperLeft.removeAttr("data-fade-out")
			$swiperRight.removeAttr("data-fade-out")
		}

		if (Math.round($container.width()) >= $track[0].scrollWidth) {
			$swiperLeft.attr("data-fade-out", "")
			$swiperRight.attr("data-fade-out", "")
		}

		translateX = targetTranslate
		lastTranslateX = translateX
		$track.css({
			transition: "transform 0.3s ease",
			transform: `translateX(${translateX}px)`
		})
	}

	function moveToItem(direction) {
		if (isMoving) return
		isMoving = true

		const wrapperWidth = $container.width()
		const containerOffset = $container.offset().left
		const containerCenter = containerOffset + wrapperWidth / 2

		let currentIndex = 0
		let closestDistance = Infinity

		// Find currently centered item
		$items.each(function (index) {
			const itemCenter = $(this).offset().left + $(this).outerWidth() / 2
			const distance = Math.abs(containerCenter - itemCenter)
			if (distance < closestDistance) {
				closestDistance = distance
				currentIndex = index
			}
		})

		// Move to the next/prev index
		const targetIndex = clamp(0, currentIndex + direction, $items.length - 1)
		const $target = $items.eq(targetIndex)
		const itemOffset = $target.position().left + $target.outerWidth() / 2

		const minTranslate = wrapperWidth - $track[0].scrollWidth
		const maxTranslate = 0
		let targetTranslate = clamp(minTranslate, wrapperWidth / 2 - itemOffset, maxTranslate)

		// Check if first or last item is in view — snap to edges
		const itemWidth = $items.first().outerWidth()

		if (targetTranslate >= maxTranslate - itemWidth / 2) {
			targetTranslate = maxTranslate // Snap to start
			// remove data-fade-out
			$swiperLeft.attr("data-fade-out", "")
			$swiperRight.removeAttr("data-fade-out")
		} else if (targetTranslate <= minTranslate + itemWidth / 2) {
			targetTranslate = minTranslate // Snap to end
			$swiperLeft.removeAttr("data-fade-out")
			$swiperRight.attr("data-fade-out", "")
		} else {
			$swiperLeft.removeAttr("data-fade-out")
			$swiperRight.removeAttr("data-fade-out")
		}

		if (Math.round($container.width()) >= $track[0].scrollWidth) {
			$swiperLeft.attr("data-fade-out", "")
			$swiperRight.attr("data-fade-out", "")
		}

		translateX = targetTranslate
		lastTranslateX = translateX

		$track.css({
			transition: "transform 0.3s ease",
			transform: `translateX(${translateX}px)`
		})

		setTimeout(() => {
			isMoving = false
		}, 300)
	}

	snapToCenter()
})

const activeRadio = {}
$("[data-toggle-radio]").each(function () {
	const $radio = $(this)

	if ($radio.hasClass("selected")) activeRadio[$radio.data("toggle-radio")] = $radio

	$radio.on("click", function () {
		if (activeRadio[$radio.data("toggle-radio")]) {
			activeRadio[$radio.data("toggle-radio")].removeClass("selected")
		}
		$radio.addClass("selected")
		activeRadio[$radio.data("toggle-radio")] = $radio

		$radio.trigger("data-toggle-radio-change")
	})
})

$("a[data-preserve-search-params]").each(function () {
	const $link = $(this)
	if (!$link.attr("href")) return

	const defaultURL = new URL($link.attr("href"), window.location.origin)
	const currentURL = new URL(window.location.href)
	for (const [key, value] of currentURL.searchParams) {
		defaultURL.searchParams.set(key, value)
	}
	$link.attr("href", defaultURL.toString())
})

$(".banner-pagination-bullet").each(function () {
	const $bullet = $(this)
	$bullet.on("data-toggle-radio-change", function (e) {
		const pageIndex = $bullet.data("banner-index")

		$(".banner").each(function () {
			const $banner = $(this)
			if ($banner.data("banner-index") == pageIndex) {
				$banner.removeAttr("data-fade-out")
			} else {
				$banner.attr("data-fade-out", "")
			}
		})
	})
})

function navigateBannerPage(direction) {
	let $target
	const $current = $("[data-toggle-radio].selected")
	if (direction === "prev") {
		$target = $current.prev()
		if ($target.length === 0) {
			$target = $("[data-toggle-radio]").last()
		}
	} else {
		$target = $current.next()
		if ($target.length === 0) {
			$target = $("[data-toggle-radio]").first()
		}
	}

	$target.trigger("click")
}

$(".banner-navigate-button").on("click", function () {
	navigateBannerPage($(this).data("banner-navigate"))
})

$(".password-show-button").each(function () {
	$(this).attr("tabindex", -1)
})

$(".password-show-button").on("change", function () {
	$(this)
		.prev()
		.prop("type", $(this).prop("checked") ? "text" : "password")
})

const toast_message = $(".toast-container").data("toast-message")
if (toast_message) {
	showToast(toast_message)
}

$(document).on("mouseenter", "[data-tooltip]", function (e) {
	const $ele = $(this)
	const text = $ele.attr("data-tooltip")
	const $tooltip = $("#tooltip")

	$tooltip.text(text)
	$tooltip
		.css({
			left: e.clientX + 10 + "px",
			top: e.clientY + 10 + "px"
		})
		.addClass("show")
})
$(document).on("mouseleave", "[data-tooltip]", function () {
	$("#tooltip").removeClass("show")
})
$(document).on("mousemove", "[data-tooltip]", function (e) {
	$("#tooltip").css({
		left: e.clientX + 10 + "px",
		top: e.clientY + 10 + "px"
	})
})

$("form").prop("noValidate", true)
$("form").prop("autocomplete", "off")

$("input[type='number']").on("keypress", function (event) {
	if (event.code === "KeyE") {
		event.preventDefault()
	}
})

$("#logout-button").on("click", async function () {
	if (await confirmation("Are you sure you want to log out?")) {
		$.ajax({
			url: "/Auth/Logout",
			type: "POST",
			success: function (data) {
				window.location.href = "/"
			}
		})
	}
})

$(".custom-select").each(function () {
	let $this = $(this)
	let $select = $this.find("select")
	let $selected = $("<div>", { class: "select-selected" }).text($select.find("option:selected").text())
	let $items = $("<div>", { class: "select-items" })

	// Build option list
	$select.find("option").each(function (i) {
		let $opt = $("<div>").text($(this).text()).attr("data-value", $(this).val())

		if ($(this).attr("selected")) {
			$opt.addClass("same-as-selected")
		}
		$opt.on("click", function () {
			$select.val($(this).attr("data-value")) // update real select tag
			$selected.text($(this).text()) // update displayed text
			setPlaceholder($select)
			$items.find(".same-as-selected").removeClass("same-as-selected")
			$(this).addClass("same-as-selected")
			$items.hide()
			$selected.removeClass("select-arrow-active")
			$select.trigger("change")
		})
		$items.append($opt)
	})

	$this.append($selected)
	$this.append($items)

	// toggle dropdown
	$selected.on("click", function (e) {
		e.stopPropagation()
		$(".select-items").not($items).hide()
		$(".select-selected").not(this).removeClass("select-arrow-active")
		$items.toggle()
		$(this).toggleClass("select-arrow-active")
	})
})

// Close dropdown if click outside
$(document).on("click", function () {
	$(".select-items").hide()
	$(".select-selected").removeClass("select-arrow-active")
})

// Set placeholder
function setPlaceholder(selectElement) {
	var selectedOption = selectElement.find(":selected")
	var searchInput = selectElement.closest("[data-manage-container='query']").find(".form-input input")
	if (searchInput.length == 0 || selectedOption.length == 0) return

	searchInput.attr("placeholder", selectedOption.text())
}
$("[data-manage-container='query'] select").each(function () {
	setPlaceholder($(this))
})

function showToast(message) {
	const toast = $('<div class="toast"></div>').text(message)
	$(".toast-container").append(toast)
	setTimeout(() => toast.addClass("show"), 100)
	setTimeout(() => {
		toast.removeClass("show")
		setTimeout(() => toast.remove(), 500)
	}, 4000)
}

function confirmation(question = "", okay = "Yes", cancel = "Cancel") {
	return new Promise((resolve) => {
		$("#confirmation-popup .title").text(question)
		$("#confirmation-popup-confirm").text(okay)
		$("#confirmation-popup-cancel").text(cancel)
		$("#confirmation-popup").addClass("show")
		$("#confirmation-popup-confirm").on("click", function () {
			$("#confirmation-popup").removeClass("show")
			resolve(true)
		})
		$("#confirmation-popup-cancel").on("click", function () {
			$("#confirmation-popup").removeClass("show")
			resolve(false)
		})
	})
}

window.confirmation = confirmation

$("form[data-expired-timestamp]").each(function () {
	const $form = $(this)
	const expiryTimestamp = Number($form.attr("data-expired-timestamp"))

	$form.find("button[type='submit'], button:not([type])").each(function () {
		const $button = $(this)
		updateButtonCountdown($button, expiryTimestamp)

		let timer = setInterval(function () {
			updateButtonCountdown($button, expiryTimestamp, timer)
		}, 1000)
	})

	$form.find("[data-expired-disable]").each(function () {
		const $button = $(this)
		updateButtonCountdown($button, expiryTimestamp, null, false)

		let timer = setInterval(function () {
			updateButtonCountdown($button, expiryTimestamp, timer, false)
		}, 1000)
	})
})

$("button[data-expired-timestamp], [data-button][data-expired-timestamp]").each(function () {
	const $button = $(this)
	const expiryTimestamp = Number($button.attr("data-expired-timestamp"))
	updateButtonCountdown($button, expiryTimestamp)

	let timer = setInterval(function () {
		updateButtonCountdown($button, expiryTimestamp, timer)
	}, 1000)
})

function updateButtonCountdown($button, expiryTimestamp, timer, changeText = true) {
	let secondsLeft = Math.floor((expiryTimestamp - Date.now()) / 1000)

	if (!$button.attr("data-original-text")) {
		$button.attr("data-original-text", $button.text())
	}

	if (secondsLeft < 0) {
		$button.attr("disabled", true)
		if ($button.attr("data-button")) {
			$button.attr("data-button", "disabled")
		}

		if (changeText) {
			$button.text("Expired")
		}
		if (timer) {
			clearInterval(timer)
		}
		return
	}

	if (changeText) {
		const minutes = String(Math.floor(secondsLeft / 60)).padStart(2, "0")
		const seconds = String(secondsLeft % 60).padStart(2, "0")

		$button.text(`${$button.attr("data-original-text")} (${minutes}:${seconds})`)
	}
}

function openOverlay(id, callback = () => {}) {
	const $overlay = $(`#${id}`)
	if (!$overlay.length) return

	$overlay.addClass("show")
	$overlay.find(".overlay-close").off("click")
	$overlay.find(".overlay-close").on("click", function () {
		$overlay.removeClass("show")
		callback()
	})

	overlayPadder($overlay)
}

function closeOverlay(id) {
	const $overlay = $(`#${id}`)
	if (!$overlay.length) return

	$overlay.removeClass("show")
}

function overlayPadder(overlayEle) {
	const $body = overlayEle.find(".overlay-body")
	if (!$body[0]) return

	if ($body[0].scrollHeight > $body[0].clientHeight) {
		$body.css("padding-right", "12px")
	} else {
		$body.css("padding-right", null)
	}
}

class UploadOverlay {
	constructor(file_input_selector, previewWidth = 250, previewHeight = 250, upload_overlay_selector = "#upload-overlay") {
		this.file_input_selector = file_input_selector
		this.upload_overlay_selector = upload_overlay_selector
		this.previewWidth = previewWidth
		this.previewHeight = previewHeight
		this.storedFiles = []

		$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").on("dragover", (event) => {
			event.preventDefault()
			$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").addClass("drag-over")
		})

		$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").on("dragleave", (event) => {
			event.preventDefault()
			$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").removeClass("drag-over")
		})

		$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").on("click", () => {
			$(this.file_input_selector).val("").click()
		})

		$(this.file_input_selector).on("click", (event) => {
			event.stopPropagation()
		})
	}

	assignFile() {
		$(this.file_input_selector).val("")
		if (this.storedFiles.length == 0) return
		const dataTransfer = new DataTransfer()
		this.storedFiles.forEach((file) => dataTransfer.items.add(file))
		$(this.file_input_selector)[0].files = dataTransfer.files
	}

	removeFile(index) {
		if (index >= 0 && index < this.storedFiles.length) {
			this.storedFiles.splice(index, 1)
			this.assignFile()
		}
	}

	removeAllFiles() {
		this.storedFiles = []
		this.assignFile()
	}

	open() {
		const previewContainer = $(this.upload_overlay_selector + ".upload-overlay .upload-preview-container")
		previewContainer.css({
			width: this.previewWidth + "px",
			height: this.previewHeight + "px"
		})

		return new Promise((resolve) => {
			const handleFile = (file) => {
				if ((file && file.type.startsWith("image/") && file.type.endsWith("jpeg")) || file.type.endsWith("png")) {
					const reader = new FileReader()

					reader.onload = async (event) => {
						const previewImage = $(this.upload_overlay_selector + ".upload-overlay .upload-preview-image")
						previewImage.attr("src", event.target.result)
						previewImage.off("load")
						previewImage.on("load", () => {
							$(this.upload_overlay_selector + ".upload-overlay .upload-preview-zone").addClass("show")
							$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").removeClass("show")

							const imageRatio = previewImage.width() / previewImage.height()
							const containerRatio = previewContainer.width() / previewContainer.height()

							let horizontalFit = false
							if (imageRatio < containerRatio) {
								horizontalFit = true
								previewImage.addClass("horizontal-fit")
								previewImage.removeClass("vertical-fit")
							} else {
								previewImage.addClass("vertical-fit")
								previewImage.removeClass("horizontal-fit")
							}

							previewImage.css("transform", `translate(-50%, -50%) scale(1)`)

							let imgX = 0
							let imgY = 0
							let scale = 1
							const baseWidth = previewImage.width()
							const baseHeight = previewImage.height()

							previewImage.off("mousedown")
							previewImage.on("mousedown", (evt) => {
								let startX = evt.clientX - imgX
								let startY = evt.clientY - imgY
								previewImage.addClass("dragging")

								$(document.body).on("mousemove", (ev) => {
									let newX = ev.clientX - startX
									let newY = ev.clientY - startY

									// Prevent moving outside container
									const maxX = (previewImage.width() * scale - previewContainer.width()) / 2
									const maxY = (previewImage.height() * scale - previewContainer.height()) / 2

									imgX = Math.min(maxX, Math.max(-maxX, newX))
									imgY = Math.min(maxY, Math.max(-maxY, newY))

									previewImage.css("transform", `translate(calc(-50% + ${imgX}px), calc(-50% + ${imgY}px)) scale(${scale})`)
								})

								$(document.body).on("mouseup", () => {
									previewImage.removeClass("dragging")
									$(document.body).off("mousemove")
									$(document.body).off("mouseup")
								})
							})

							$(".zoom-slider").val(1)
							$(".zoom-slider").off("input")
							$(".zoom-slider").on("input", (evt) => {
								const prevScale = scale
								scale = parseFloat(evt.target.value)

								const scaleRatio = scale / prevScale
								imgX *= scaleRatio
								imgY *= scaleRatio

								const maxX = Math.max(0, (baseWidth * scale - previewContainer.width()) / 2)
								const maxY = Math.max(0, (baseHeight * scale - previewContainer.height()) / 2)

								imgX = Math.min(maxX, Math.max(-maxX, imgX))
								imgY = Math.min(maxY, Math.max(-maxY, imgY))

								previewImage.css("transform", `translate(calc(-50% + ${imgX}px), calc(-50% + ${imgY}px)) scale(${scale})`)
							})

							$(".confirm-upload").off("click")
							$(".confirm-upload").on("click", () => {
								$(this.upload_overlay_selector + ".upload-overlay").removeClass("show")
								this.storedFiles.push(file)
								this.assignFile()
								resolve({
									src: event.target.result,
									scale: scale,
									imgX: imgX,
									imgY: imgY,
									horizontalFit: horizontalFit,
									index: this.storedFiles.length - 1
								})
							})
						})
					}

					reader.readAsDataURL(file)
				}
			}

			$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").addClass("show")
			$(this.upload_overlay_selector + ".upload-overlay .upload-preview-zone").removeClass("show")
			$(this.upload_overlay_selector + ".upload-overlay").addClass("show")

			$(this.upload_overlay_selector + ".upload-overlay .overlay-close").off("click")
			$(this.upload_overlay_selector + ".upload-overlay .overlay-close").on("click", () => {
				$(`.upload-overlay`).removeClass("show")
				this.assignFile()
				resolve(false)
			})

			$(this.file_input_selector).off("change")
			$(this.file_input_selector).on("change", (e) => {
				const file = e.target.files[0]
				handleFile(file)
			})

			$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").off("drop")
			$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").on("drop", (event) => {
				event.preventDefault()
				$(this.upload_overlay_selector + ".upload-overlay .upload-drop-zone").removeClass("drag-over")

				const file = event.originalEvent.dataTransfer.files[0]
				handleFile(file)
			})
		})
	}
}

function updateImagePreview(selector, x, y, scale) {
	$(selector).css("transform", `translate(calc(-50% + ${x}px), calc(-50% + ${y}px)) scale(${scale})`)
}

function detectFormChanges(formSelector, selectElements = "input", changedCallback = () => {}, unchangedCallback = () => {}) {
	let form = $(formSelector)
	let original_data = form.serialize()

	$(`${formSelector}`).on("change input", selectElements, function () {
		if (form.serialize() === original_data) {
			unchangedCallback()
		} else {
			changedCallback()
		}
	})

	return function () {
		form = $(formSelector)
		original_data = form.serialize()
		unchangedCallback()
	}
}

function toRMFormat(value) {
	return value.toFixed(2)
}

function toRatingFormat(value) {
	return value.toFixed(1)
}

function toDateFormat(dateStr) {
	const months = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]
	const [year, month, day] = dateStr.split("-")
	return `${day} ${months[parseInt(month) - 1]} ${year}`
}

function timeAgo(datetime) {
	const formattedDatetime = datetime.replace(" ", "T")
	const now = new Date()
	const past = new Date(formattedDatetime)

	if (isNaN(past)) return "Invalid date"

	const seconds = Math.floor((now - past) / 1000)

	const intervals = {
		year: 31536000,
		month: 2592000,
		week: 604800,
		day: 86400,
		hour: 3600,
		minute: 60,
		second: 1
	}

	for (const unit in intervals) {
		const count = Math.floor(seconds / intervals[unit])
		if (count >= 1) {
			return `${count} ${unit}${count > 1 ? "s" : ""} ago`
		}
	}

	return "just now"
}

$("[data-clear-form]").on("click", function () {
	const $form = $(this).closest("form")
	if (!$form.length) return

	$form
		.find("input, select, textarea")
		.not("[data-clear-form-ignore]")
		.each(function () {
			if (this.disabled || this.readOnly) return

			switch (this.type) {
				case "checkbox":
				case "radio":
					this.checked = false
					break
				case "select-one":
				case "select-multiple":
					this.selectedIndex = -1 // clears selection
					break
				default:
					this.value = ""
			}
		})
})

$(document).on("click", "[data-collapsible-toggle]", function () {
	$(this).toggleClass("collapsed")
})

function ensureTwoDigitsAfterDecimal(value) {
	const decimalString = value.toString()
	const decimalPointLoc = decimalString.indexOf(".")

	if (decimalPointLoc !== -1) {
		const numberOfDecimalDigits = decimalString.length - (decimalPointLoc + 1)
		if (numberOfDecimalDigits === 1) {
			return decimalString + "0"
		}
		return decimalString
	} else {
		return decimalString + ".00"
	}
}
function formatCents(cents) {
	if (cents !== "") {
		return ensureTwoDigitsAfterDecimal(cents / 100)
	} else {
		return ""
	}
}

$("input[data-rm-format]").on("input", function () {
	const value = $(this)
		.val()
		.replace(/[^0-9]/g, "")
	if (value !== "") {
		$(this).val(formatCents(value))
	}

	if ($(this).attr("data-rm-format") == "nullable") {
		if (Number(value) === 0) {
			$(this).val(null)
		}
	}
})

$("[data-auto-submit]").on("change", function () {
	$(this).closest("form").submit()
})

$("#filter").on("click", function () {
	openOverlay("filter-overlay")
})

function defaultOnFailure(xhr, status, error) {
	showToast(xhr.responseText)
}

function reloadPage() {
	window.location.reload()
}

function redirectToPage(url) {
	window.location.href = url
}

const accountConnection = new signalR.HubConnectionBuilder().withUrl("/AccountHub").build()
var accountConnectionId = null
var accountConnectionToken = null

accountConnection.on("Error", (message) => showToast(message))

accountConnection.on("Initialize", (accountId, sessionToken) => {
	if (accountId == null || sessionToken == null) return

	accountConnectionId = accountId
	accountConnectionToken = sessionToken
})

accountConnection.on("Logout", (sessionToken) => {
	if (sessionToken == null || sessionToken != accountConnectionToken) return

	handleLoggedOut()
})

accountConnection.on("LogoutAll", (accountId) => {
	if (accountId == null || accountId != accountConnectionId) return

	handleLoggedOut()
})

accountConnection.start().then(() => {
	accountConnection.invoke("Initialize")
})

function notifyLoggedOut() {
	showToast("You've been logged out. Reload to take effect.")
}

function handleLoggedOut() {
	if (document.hidden) {
		document.addEventListener("visibilitychange", function onVisible() {
			if (!document.hidden) {
				notifyLoggedOut()
				document.removeEventListener("visibilitychange", onVisible) // remove listener after first run
			}
		})
	}
}
