(function () {
    const subject = document.getElementById("Compose_Subject");
    const body = document.getElementById("Compose_Body");
    const previewSubject = document.getElementById("preview-subject");
    const previewBody = document.getElementById("preview-body");
    const selectAll = document.getElementById("select-all");
    const sendForm = document.getElementById("send-form");

    function value(id) {
        const element = document.getElementById(id);
        return element ? element.value.trim() : "";
    }

    function company() {
        const picked = document.querySelector(".companies tbody tr .pick:checked");
        const row = picked ? picked.closest("tr") : null;
        return row ? row.dataset.company : "the company";
    }

    function fill(template) {
        const tokens = {
            CompanyName: company(),
            YourName: value("Compose_YourName"),
            Role: value("Compose_Role"),
            Phone: value("Compose_Phone"),
            Link: value("Compose_Link"),
            Extra: value("Compose_Extra")
        };
        return template.replace(/\{\{\s*(\w+)\s*\}\}/g, function (_, key) {
            return Object.prototype.hasOwnProperty.call(tokens, key) ? tokens[key] : "";
        }).replace(/[ \t]+\n/g, "\n").replace(/(\n\s*){3,}/g, "\n\n").trim();
    }

    function renderPreview() {
        if (!subject || !body || !previewSubject || !previewBody) {
            return;
        }
        previewSubject.textContent = fill(subject.value).replace(/\s+/g, " ");
        previewBody.textContent = fill(body.value);
    }

    document.addEventListener("input", renderPreview);
    document.addEventListener("change", function (event) {
        if (event.target.classList && event.target.classList.contains("pick")) {
            if (selectAll) {
                const boxes = document.querySelectorAll(".pick");
                selectAll.checked = Array.from(boxes).every(function (box) { return box.checked; });
            }
        }
        renderPreview();
    });

    if (selectAll) {
        selectAll.addEventListener("change", function () {
            document.querySelectorAll(".pick").forEach(function (box) {
                box.checked = selectAll.checked;
            });
            renderPreview();
        });
    }

    if (sendForm) {
        sendForm.addEventListener("submit", function (event) {
            const submitter = event.submitter;
            if (!submitter || submitter.id !== "send-btn") {
                return;
            }
            submitter.disabled = true;
            submitter.textContent = "Sending...";
        });
    }

    renderPreview();
})();
