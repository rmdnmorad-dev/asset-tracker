# ISAT Careers — Power Pages application portal

A complete, mobile-first careers portal for **Microsoft Power Pages** with the
simplicity of Microsoft Forms:

| File | What it is | Suggested page URL |
|------|-----------|--------------------|
| `landing-page.html` | Marketing/home page (your original design, made responsive + wired to the form) | `/` (Home) |
| `application-form.html` | Multi-step applicant form with **CV upload** | `/apply` |
| `admin-dashboard.html` | Recruiter view: **KPIs, charts, filter/search/sort table** | `/admin` |

Each file is a **self-contained** block of HTML + CSS + JavaScript — no external
libraries, so nothing is blocked by the Power Pages content-security policy. The
charts are hand-drawn with inline SVG.

---

## 1. Try it now (Demo mode — zero setup)

Both data pages ship with `DEMO_MODE = true`.

1. In the Power Pages **design studio**, create three web pages with the URLs above.
2. For each page, open the **`</>` (Edit code / source)** view and paste the matching file.
3. **Sync** and browse the site:
   - `/apply` — fill in the form on your phone or desktop and submit a test application.
   - `/admin` — the dashboard shows sample data **plus** anything you just submitted.

In demo mode, applications are stored in the **browser** (`localStorage`), so the
form and dashboard talk to each other on the same device. This is purely for
previewing the experience — flip to live mode below to store data in Dataverse.

> The form works great on mobile: 16px inputs (no iOS zoom), large tap targets,
> a sticky Next/Back bar, and drag-&-drop or tap-to-upload for the CV.

---

## 2. Go live with Dataverse

### 2.1 Create the table

Create a Dataverse table — e.g. **Job Applicant** (`isat_jobapplicant`). Replace
the `isat_` prefix with your own publisher prefix everywhere if it differs, and
update the `CONFIG.columns` map at the top of **both** HTML files to match.

| Field on the form | Column display name | Logical name | Type |
|---|---|---|---|
| Full name | Full Name | `isat_fullname` | Text (Primary) |
| Email | Email | `isat_email` | Email |
| Phone | Phone | `isat_phone` | Phone |
| City | City | `isat_city` | Text |
| LinkedIn / Portfolio | LinkedIn | `isat_linkedin` | URL |
| Position | Position | `isat_position` | Text *(or Choice)* |
| Department | Department | `isat_department` | Text *(or Choice)* |
| Employment type | Employment Type | `isat_employmenttype` | Text *(or Choice)* |
| Preferred location | Work Location | `isat_worklocation` | Text *(or Choice)* |
| Years of experience | Experience | `isat_experience` | Text *(or Choice)* |
| Highest education | Education | `isat_education` | Text *(or Choice)* |
| Current title | Current Title | `isat_currenttitle` | Text |
| Availability | Availability | `isat_availability` | Text *(or Choice)* |
| Cover letter | Cover Letter | `isat_coverletter` | Multiline Text |
| Status | Status | `isat_status` | Choice: `New, Reviewing, Interview, Offer, Hired, Rejected` |
| CV | CV | `isat_cv` | **File** *(if using the File-column CV option)* |
| Applied on | *(built-in)* | `createdon` | Created On |

> **Tip:** the code sends plain text for the choice-style fields. Keeping them as
> **Text** columns is the simplest path. If you prefer real **Choice** columns,
> send the numeric option value instead of the label (see comments in the code),
> or map them.

### 2.2 Create the pages

Create the three web pages, paste each file into its `</>` source view, and note
each page's **partial URL** (`apply`, `admin`, …). If your form page is not at
`/apply`, update the `href="/apply"` links in `landing-page.html`.

### 2.3 Enable the Web API

In **Power Pages → Settings → Site settings** (or the Portal Management app), add:

| Name | Value |
|---|---|
| `Webapi/isat_jobapplicant/enabled` | `true` |
| `Webapi/isat_jobapplicant/fields` | `*` |

(If you use the Notes option for the CV, also add
`Webapi/annotation/enabled = true` and `Webapi/annotation/fields = *`.)

### 2.4 Table permissions & web roles

Create **Table Permissions** (Power Pages → Set up → Table permissions):

| Purpose | Table | Access | Scope | Web role |
|---|---|---|---|---|
| Applicants submit | Job Applicant | **Create** (+ *Write* only if using the File-column CV) | Global | **Anonymous Users** |
| Recruiters review | Job Applicant | **Read, Write** | Global | **Recruiter** *(create this role)* |

For the **Notes** CV option, add a child permission on **Note (annotation)** with
**Create/Append To** for Anonymous, related via the applicant table.

### 2.5 Secure the admin page 🔒

The dashboard must **not** be public. Restrict `/admin`:

- Create a web role, e.g. **Recruiter**, and assign your HR/admin contacts to it.
- Add a **Page Permission / Web Page Access Control Rule** on the `/admin` page:
  **Restrict Read** and grant to the **Recruiter** role only.

### 2.6 Flip the switch

In **both** `application-form.html` and `admin-dashboard.html`, set:

```js
DEMO_MODE: false
```

and confirm `entitySet`, `columns`, and `cvColumn` match your table. Sync — the
form now writes to Dataverse and the dashboard reads live records.

---

## 3. CV upload — pick one option

The form supports two upload methods via `CONFIG.cvMode`:

| `cvMode` | How it stores the CV | Anonymous permission needed | Dashboard reading |
|---|---|---|---|
| `"file"` *(default)* | Dataverse **File column** `isat_cv` | Create **+ Write** on the table | Direct link: `…(id)/isat_cv/$value` — already wired |
| `"notes"` | Classic **Note/annotation** | Create on **annotation** only (safer) | Query/expand the record's notes (see below) |

- **`file`** is the simplest end-to-end and needs no extra reading code, but
  granting anonymous *Write* on the table is broader access. Prefer it when
  applicants **sign in** before applying, or when the table is a write-only inbox.
- **`notes`** follows the classic Power Pages "attach file" pattern and only needs
  anonymous **Create on annotation**. To show these CVs in the dashboard, read the
  related notes, e.g.:

  ```
  GET /_api/isat_jobapplicants?$select=…&$expand=isat_jobapplicant_Annotations($select=annotationid,filename)
  ```

  then link each file to `/_api/annotations(<annotationid>)/documentbody/$value`.

**No-code fallback:** if you'd rather not manage Web API permissions, submit the
record with a **Basic Form** (Power Pages form component) that has *Attach File*
enabled — it writes to Dataverse and stores the CV as a Note natively. You can
still use `admin-dashboard.html` for the recruiter view. The custom form here is
for the Microsoft-Forms look and multi-step flow.

**Limits:** the form caps CVs at **5 MB** and accepts **PDF / DOC / DOCX**
(`MAX_CV_BYTES` and the `accept` list in the code).

---

## 4. What the recruiter dashboard does

- **KPI tiles:** total applicants, new this week, in-progress, hired (+ hire rate).
- **Charts** (inline SVG, hover tooltips): applications over time (8-week trend),
  by position, by department, and a status **pipeline** (New → Hired, plus Rejected).
- **Table:** search (name / email / position), filter by position / department /
  status, and sort any column. Change an applicant's **status** inline, open a
  **details drawer**, download the **CV**, or email the applicant.
- **Export CSV** of the current filtered view.

Colours use a validated, colour-blind-safe blue ramp; status is shown with a
labelled pill (never colour alone).

---

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| `403` on submit | Table Permission **Create** missing for the web role, or Web API not enabled for the table. |
| `403`/empty on dashboard | Recruiter role missing **Read**, or the user isn't in the role. |
| Token error on submit | Power Pages injects `__RequestVerificationToken` when the page has a form. Keep the page as a standard web page; the code also falls back to the token endpoint. |
| CV not saved but record is | Expected if using `file` mode without anonymous **Write** — switch to `notes` mode or grant Write. The record is kept either way. |
| Choice column rejected | Send the numeric option value, or keep the column as **Text**. |
| Charts look empty | No records match yet — submit a few, or check `DEMO_MODE`. |

---

## 6. Customising

- **Positions / dropdown options:** edit the chips and `<option>`s in
  `application-form.html` (Step 1–3).
- **Brand colours:** the `--brand`, `--brand-2`, `--accent` CSS variables at the
  top of each file (ISAT navy / blue / orange by default).
- **Statuses:** change the `STATUSES` array in `admin-dashboard.html` and the
  matching Choice column values.
