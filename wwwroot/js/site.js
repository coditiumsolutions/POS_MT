(function () {
    var sidebar = document.getElementById("posSidebar");
    var backdrop = document.getElementById("posBackdrop");
    var toggle = document.getElementById("posSidebarToggle");
    var storageKey = "posSidebarCollapsed";

    function setCollapsed(collapsed) {
        if (!sidebar || !toggle) {
            return;
        }

        sidebar.classList.toggle("collapsed", collapsed);
        toggle.setAttribute("aria-label", collapsed ? "Expand sidebar" : "Collapse sidebar");
        toggle.setAttribute("title", collapsed ? "Expand sidebar" : "Collapse sidebar");
        toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");

        var icon = toggle.querySelector("i");
        if (icon) {
            icon.className = collapsed ? "bi bi-chevron-right" : "bi bi-list";
        }

        try {
            localStorage.setItem(storageKey, collapsed ? "1" : "0");
        } catch (e) {
            /* ignore storage errors */
        }
    }

    function closeSidebar() {
        if (!sidebar || !backdrop) {
            return;
        }

        sidebar.classList.remove("open");
        backdrop.classList.remove("show");
    }

    if (backdrop) {
        backdrop.addEventListener("click", closeSidebar);
    }

    if (sidebar && toggle) {
        var saved = false;
        try {
            saved = localStorage.getItem(storageKey) === "1";
        } catch (e) {
            saved = false;
        }

        setCollapsed(saved);

        toggle.addEventListener("click", function () {
            setCollapsed(!sidebar.classList.contains("collapsed"));
        });
    }

    window.posDataTable = function (selector, options) {
        if (!window.jQuery || !jQuery.fn.DataTable) {
            return;
        }

        options = options || {};
        var lengthBelow = !!options.lengthBelow;
        var hideFilter = !!options.hideFilter;
        delete options.lengthBelow;
        delete options.hideFilter;

        var config = {
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            order: [],
            autoWidth: false
        };

        // Bootstrap5 integration sets a default dom with "f" (search). Override completely when needed.
        if (lengthBelow && hideFilter) {
            config.dom =
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row dt-controls-bottom align-items-center mt-2'<'col-sm-12 col-md-4'l><'col-sm-12 col-md-4'i><'col-sm-12 col-md-4'p>>";
        } else if (lengthBelow) {
            config.dom =
                "<'row'<'col-sm-12 col-md-6'f>>" +
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row dt-controls-bottom align-items-center mt-2'<'col-sm-12 col-md-4'l><'col-sm-12 col-md-4'i><'col-sm-12 col-md-4'p>>";
        } else if (hideFilter) {
            config.dom =
                "<'row'<'col-sm-12 col-md-6'l>>" +
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>";
        }

        jQuery.extend(true, config, options);

        // Ensure Bootstrap default search slot cannot come back.
        if (hideFilter && typeof config.dom === "string") {
            config.dom = config.dom.replace(/f/g, "");
        }

        return jQuery(selector).DataTable(config);
    };

    function closeTopDropdowns(except) {
        document.querySelectorAll("[data-top-dropdown]").forEach(function (dropdown) {
            if (except && dropdown === except) {
                return;
            }
            dropdown.classList.remove("open");
            var menu = dropdown.querySelector(".pos-top-dropdown-menu");
            var twistee = dropdown.querySelector("[data-top-twistee]");
            if (menu) {
                menu.hidden = true;
            }
            if (twistee) {
                twistee.setAttribute("aria-expanded", "false");
            }
        });
    }

    document.querySelectorAll("[data-top-twistee]").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            var dropdown = btn.closest("[data-top-dropdown]");
            if (!dropdown) {
                return;
            }
            var willOpen = !dropdown.classList.contains("open");
            closeTopDropdowns(willOpen ? dropdown : null);
            dropdown.classList.toggle("open", willOpen);
            var menu = dropdown.querySelector(".pos-top-dropdown-menu");
            if (menu) {
                menu.hidden = !willOpen;
            }
            btn.setAttribute("aria-expanded", willOpen ? "true" : "false");
        });
    });

    document.querySelectorAll("[data-side-nav-twistee]").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            var group = btn.closest("[data-side-nav-group]");
            if (!group) {
                return;
            }
            var willOpen = !group.classList.contains("open");
            group.classList.toggle("open", willOpen);
            btn.setAttribute("aria-expanded", willOpen ? "true" : "false");
        });
    });

    document.addEventListener("click", function () {
        closeTopDropdowns(null);
    });

    (function initHubStatusBadge() {
        var badge = document.querySelector("[data-hub-status]");
        if (!badge) {
            return;
        }

        var textEl = badge.querySelector(".hub-status-text");
        var pollMs = 15000;
        var timer = null;

        function setBadge(cssClass, label, title) {
            badge.className = "hub-status-badge " + cssClass;
            if (textEl) {
                textEl.textContent = label;
            }
            badge.title = title || label;
        }

        function pickClass(data) {
            var sync = (data.syncStatus || "").toLowerCase();
            if (sync.indexOf("fail") >= 0) {
                return "hub-status-failed";
            }
            if (sync === "syncing" || sync === "pending") {
                return "hub-status-syncing";
            }
            if ((data.centralReachable === true) || (data.connectivity || "").toLowerCase() === "online") {
                return "hub-status-online";
            }
            return "hub-status-offline";
        }

        // Badge shows sync/connectivity only — Offline/Online/Auto is the Mode dropdown label.
        function pickLabel(data) {
            var sync = (data.syncStatus || "").toLowerCase();
            if (sync.indexOf("fail") >= 0) {
                return "Sync Failed";
            }
            if (sync === "syncing") {
                return "Syncing";
            }
            if (sync === "pending") {
                return "Pending";
            }
            if (sync === "synced") {
                return "Synced";
            }
            if (sync === "n/a" || sync === "") {
                return data.centralReachable === false ? "No central" : "Ready";
            }
            return data.syncStatus || "Ready";
        }

        function markWorkModeButtons(workMode) {
            var modeName = workMode || "Auto";
            document.querySelectorAll("[data-work-mode]").forEach(function (btn) {
                var mode = btn.getAttribute("data-work-mode") || "";
                btn.classList.toggle("active", mode.toLowerCase() === modeName.toLowerCase());
            });
            var label = document.querySelector("[data-work-mode-label]");
            if (label) {
                label.textContent = "Mode: " + modeName;
            }
        }

        function refresh() {
            fetch("/api/hub/status", {
                method: "GET",
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            })
                .then(function (res) {
                    if (!res.ok) {
                        throw new Error("Status " + res.status);
                    }
                    return res.json();
                })
                .then(function (data) {
                    if (data.pollSeconds) {
                        pollMs = Math.max(5, Number(data.pollSeconds)) * 1000;
                    }
                    var label = pickLabel(data);
                    var title = [
                        "Work mode: " + (data.workMode || ""),
                        "Active DB: " + (data.activeDatabase || ""),
                        data.routingMessage || "",
                        data.localDatabaseMessage || "",
                        data.centralMessage || "",
                        "Sync: " + (data.syncStatus || ""),
                        "Pending: " + (data.pendingCount || 0),
                        "Failed: " + (data.failedCount || 0)
                    ].join("\n");
                    setBadge(pickClass(data), label, title);
                    markWorkModeButtons(data.workMode);
                })
                .catch(function () {
                    setBadge("hub-status-unknown", "Status ?", "Unable to read hub status");
                })
                .finally(function () {
                    if (timer) {
                        clearTimeout(timer);
                    }
                    timer = setTimeout(refresh, pollMs);
                });
        }

        function showModeMessage(text, isError) {
            var box = document.getElementById("hubModeMessage");
            if (!box) {
                alert(text);
                return;
            }
            box.hidden = false;
            box.textContent = text;
            box.style.color = isError ? "#fde68a" : "#bbf7d0";
            if (showModeMessage._timer) {
                clearTimeout(showModeMessage._timer);
            }
            showModeMessage._timer = setTimeout(function () {
                box.hidden = true;
            }, 12000);
        }

        document.querySelectorAll("[data-work-mode]").forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                e.stopPropagation();
                var mode = btn.getAttribute("data-work-mode");
                var host = (window.location.hostname || "").toLowerCase();
                var isLocalHost = host === "localhost" || host === "127.0.0.1";

                // Cloud website cannot go offline — browser needs internet to reach it at all.
                if (mode === "Offline" && !isLocalHost) {
                    showModeMessage(
                        "Offline does not work on this cloud site. Open Local Hub: http://localhost:5227 then click Offline (or leave Auto).",
                        true
                    );
                    return;
                }

                var meta = document.querySelector('meta[name="request-verification-token"]');
                var token = meta ? meta.getAttribute("content") : "";
                fetch("/api/hub/work-mode", {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "Accept": "application/json",
                        "RequestVerificationToken": token || ""
                    },
                    body: JSON.stringify({ mode: mode })
                })
                    .then(function (res) {
                        return res.json().then(function (body) { return { ok: res.ok, body: body }; }).catch(function () {
                            return { ok: res.ok, body: { message: "Could not change mode (HTTP " + res.status + ")." } };
                        });
                    })
                    .then(function (result) {
                        if (!result.ok) {
                            showModeMessage((result.body && result.body.message) || "Could not change mode.", true);
                            return;
                        }
                        showModeMessage((result.body && result.body.message) || ("Switched to " + mode + "."), false);
                        markWorkModeButtons(mode);
                        closeTopDropdowns(null);
                        // Reload so lists (e.g. Sales Invoices) load from the newly selected database.
                        window.location.reload();
                    })
                    .catch(function () {
                        showModeMessage("Could not change mode. If this is the cloud site, use http://localhost:5227 instead.", true);
                    });
            });
        });

        refresh();
    })();

    (function ensureMonthlySupplyExpandUi() {
        function run() {
            var table = document.getElementById("monthlySupplyTable");
            if (!table) {
                return;
            }

            var rows = table.querySelectorAll(".monthly-supply-customer-row");
            if (!rows.length) {
                return;
            }

            if (rows[0].querySelector(".monthly-supply-twistee")) {
                return;
            }

            var headerRow = table.querySelector("thead tr");
            if (headerRow && !headerRow.querySelector(".monthly-supply-expand-col")) {
                var th = document.createElement("th");
                th.className = "monthly-supply-expand-col";
                th.style.width = "40px";
                th.title = "Expand row for monthly items";
                th.innerHTML = "<span class=\"monthly-supply-expand-hdr\" aria-hidden=\"true\">&#9654;</span><span class=\"visually-hidden\">Expand</span>";
                if (headerRow.children.length > 1) {
                    headerRow.insertBefore(th, headerRow.children[1]);
                } else {
                    headerRow.appendChild(th);
                }
            }

            rows.forEach(function (row) {
                if (row.querySelector(".monthly-supply-twistee")) {
                    return;
                }
                var td = document.createElement("td");
                td.className = "monthly-supply-expand-cell";
                td.innerHTML =
                    "<button type=\"button\" class=\"monthly-supply-twistee\" aria-expanded=\"false\" title=\"Show monthly items\">" +
                    "<span class=\"monthly-supply-twistee-glyph\" aria-hidden=\"true\">&#9654;</span></button>";
                if (row.children.length > 1) {
                    row.insertBefore(td, row.children[1]);
                } else {
                    row.appendChild(td);
                }

                var btn = td.querySelector(".monthly-supply-twistee");
                btn.addEventListener("click", function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    row.dispatchEvent(new MouseEvent("dblclick", { bubbles: true, cancelable: true }));
                });
            });

            var headerCols = table.querySelectorAll("thead th").length;
            table.querySelectorAll(".monthly-supply-detail-row td[colspan]").forEach(function (cell) {
                if (headerCols > 0) {
                    cell.colSpan = headerCols;
                }
            });
        }

        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", run);
        } else {
            run();
        }
    })();

    if ("serviceWorker" in navigator) {
        window.addEventListener("load", function () {
            navigator.serviceWorker.register("/sw.js").catch(function () {
                /* ignore SW registration failures */
            });
        });
    }
})();
