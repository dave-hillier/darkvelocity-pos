# Lightspeed Restaurant K-Series - Functional Specification

**Generated from:** Official Lightspeed Getting Started Video Series (21 videos)
**Source Playlist:** https://www.youtube.com/playlist?list=PL3VFXcsYQuclGjpkL8q1xPVgEi6jcRj0v

---

## Table of Contents

1. [Overview](#overview)
2. [System Architecture](#system-architecture)
3. [Back Office](#back-office)
4. [POS Application](#pos-application)
5. [User Management](#user-management)
6. [Menu & Item Management](#menu--item-management)
7. [Floor Plans & Table Management](#floor-plans--table-management)
8. [Order Processing](#order-processing)
9. [Payment Processing](#payment-processing)
10. [Hardware Integration](#hardware-integration)
11. [Tax Configuration](#tax-configuration)
12. [Multi-Location Management](#multi-location-management)
13. [Reporting & Analytics](#reporting--analytics)

---

## Overview

Lightspeed Restaurant K-Series is a cloud-based point-of-sale (POS) system designed for restaurants, cafes, bars, and food service businesses. The system consists of two main components:

1. **Back Office** - Web-based administration portal for configuration, reporting, and management
2. **POS App** - iPad application for taking orders and processing payments

### Key Benefits
- Process orders faster
- Streamline operations
- Improve customer experience
- Track employee hours and sales
- Manage multiple locations from a single platform

### Prerequisites
- Lightspeed Restaurant account
- iPad device for POS application
- Computer for Back Office access (recommended)
- Internet connection and dedicated router
- Compatible hardware (printers, cash drawers, payment terminals)

---

## System Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                      BACK OFFICE                            │
│  (Web Application - accessed via computer)                  │
│  ┌──────────────┬──────────────┬──────────────┐            │
│  │ Configuration│   Reporting  │   Hardware   │            │
│  │   Settings   │   Analytics  │  Management  │            │
│  └──────────────┴──────────────┴──────────────┘            │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Cloud Sync
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    POS APPLICATION                          │
│  (iPad App - Restaurant Floor)                              │
│  ┌──────────────┬──────────────┬──────────────┐            │
│  │  Home Screen │   Register   │   Settings   │            │
│  │  Clock In/Out│   Ordering   │   Cash Drawer│            │
│  └──────────────┴──────────────┴──────────────┘            │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Connected Hardware
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      HARDWARE                               │
│  ┌─────────┬─────────┬─────────┬─────────┬─────────┐       │
│  │ Receipt │ Kitchen │  Cash   │ Payment │ Barcode │       │
│  │ Printer │ Printer │ Drawer  │Terminal │ Scanner │       │
│  └─────────┴─────────┴─────────┴─────────┴─────────┘       │
└─────────────────────────────────────────────────────────────┘
```

### Setup Mode
- New accounts start in "Setup Mode"
- Allows processing test orders without affecting business reporting
- Setup guide tracks main tasks to complete before launch
- Exit setup mode to begin processing actual customer orders

![Setup Lightspeed Restaurant](screenshots/02-backoffice-setup-lightspeed.jpg)

---

## Back Office

### Access & Authentication
- **URL**: Provided in registration email
- **Username**: Email address used for registration or employee profile email
- **Password**: Set during initial account creation
- **Password Reset**: Available via "Forgot Password" link

### Navigation Structure

```
Sidebar Navigation:
├── Configuration
│   ├── Settings
│   │   ├── Business Settings
│   │   ├── Payment Methods
│   │   └── Floor Plans
│   ├── Configurations (POS device settings)
│   └── Users
│       ├── POS Users
│       └── POS User Groups
├── Menu
│   ├── Item List
│   ├── Menu Management
│   └── Accounting Groups
├── Payment
│   ├── Taxes
│   └── Tax Profiles
├── Hardware
│   ├── Printers
│   └── Cash Drawers
├── Financial Services
│   └── Terminals
├── Reports
│   └── Cash Drawer Report
└── POS
    └── Configuration
```

![Back Office Header and Navigation](screenshots/05-backoffice-header-bar.jpg)

### Business Settings

**Location**: Configuration > Settings > Business Settings

![Business Settings Navigation](screenshots/06-business-settings-navigation.jpg)

#### Business Details Tab
- Internal business name (not visible to customers)
- Trade and company details
- Fiscal details (VAT, GST, business number) - region dependent
- Fiscalization requirements (region dependent)

![Business Details Tab](screenshots/07-business-details-tab.jpg)

#### Business Type Selection

![Business Type Selection](screenshots/04-backoffice-dashboard-sidebar.jpg)

#### Basic Settings Tab
- **Employee Time Tracking**: Toggle on/off for clock in/out at POS
- **Tax in Cost Calculation**:
  - Enabled: Margins = Sale Price - Cost Price
  - Disabled: Margins = (Sale Price - Tax) - Cost Price

![Basic Settings Tab](screenshots/08-basic-settings-tab.jpg)

---

## POS Application

### Home Screen

The home screen serves as the main entry point for employees and varies based on sales period status.

#### When Sales Period is Closed
- Displays POS device information
- Only option: Manager clock in to open new sales period

![Home Screen - Closed Period](screenshots/10-pos-home-closed-period.jpg)

#### When Sales Period is Open
- **Clock In/Out Button**: Start/end employee shifts
- **View Floor Plan**: Quick access to table layout
- **Log In**: Access to POS register
- **About**: Device and connection information

![Home Screen - Open Period with Users](screenshots/11-pos-home-open-period.jpg)

### Clock In/Out Interface

![Clock In/Out Interface](screenshots/12-pos-clock-in-out.jpg)

### POS Login with PIN

![POS Login Screen](screenshots/13-pos-login-screen.jpg)

### Home Screen - About Section
Displays:
- Connected Lightspeed account
- Device name
- POS configuration
- App version
- WiFi network
- IP address
- Account sharing role

![About Section](screenshots/15-pos-about-section.jpg)

### Floor Plan View from Home

![Floor Plan View](screenshots/14-pos-view-floor-plan.jpg)

### Register Screen

The main ordering interface with:
- Menu categories and item buttons
- Keypad for quantities
- Order summary
- Quick pay buttons
- Pay button (opens payment screen)
- Split check options (if enabled)

![Register Screen - Direct Sales](screenshots/40-register-direct-sales.jpg)

### Settings Screen

Access via logged-in POS user:
- Cash drawer management
- Device settings
- Report printing

---

## User Management

### User Types

1. **POS Users** - Employees who interact with the POS app
2. **Back Office Users** - Employees who access the back office (separate accounts)

### POS User Creation

**Location**: Configuration > Users > POS Users

![POS Users List](screenshots/17-pos-users-list.jpg)

#### Required Fields
- **Username**: Display name on POS (initials, ID number, etc.)
- **First Name**
- **Last Name**
- **User Group**: Determines permissions

![Add New User Form](screenshots/18-add-new-user-form.jpg)

#### Authentication Methods
- **PIN Code**: 4-digit code entered via keypad
- **QR Code**: Printed/downloaded code scanned by POS camera

![User PIN Setup](screenshots/20-user-pin-setup.jpg)

![User QR Code Setup](screenshots/21-user-qr-code.jpg)

### Pre-defined User Groups

| Group | Description | Use Case |
|-------|-------------|----------|
| Clock-in Only | Cannot log in or process orders | Back of house staff (chefs, etc.) |
| Staff | Basic permissions, excludes high-level features | Servers, front of house staff |
| Managers | Full permissions including admin access | Shift managers, owners |

![User Group Selection](screenshots/19-user-group-dropdown.jpg)

### User Group Permissions

**Location**: Configuration > Users > POS User Groups

Configurable settings:
- Access and user permissions
- Report handling
- Tipping settings
- Cash drawer access

### Multi-Location User Access
- POS users are shared across all connected locations
- Users can only access their "home" location by default
- Additional location access must be explicitly granted
- Edit via: Actions > Edit Business Location

---

## Menu & Item Management

### Item Types

1. **Items**: Individual products (burger, pizza slice, sandwich)
2. **Item Groups**: Sets where customers choose one or more options (side dish selection)
3. **Combos**: Sets sold together at fixed price (meal deals)

### Creating Items

**Location**: Menu > Item List

![Item List Page](screenshots/27-item-list-page.jpg)

#### Single Item Creation
- Click Create > Item
- Enter name, price, accounting group
- Additional fields: allergy info, production instructions, barcodes, ingredients, inventory tracking

![Create Item Options](screenshots/28-create-item-options.jpg)

#### Bulk Item Creation
- Click Create > Multiple Items
- Manual import format: name, price, accounting group (one per line)
- Optional fields: statistic group, screen

![Multiple Items Import](screenshots/29-multiple-items-import.jpg)

#### Accounting Groups
- Required for each item
- Used for:
  - Tax application
  - Report generation
  - Sales comparison
- Default groups provided or create custom groups

![Accounting Groups](screenshots/30-accounting-groups.jpg)

### Menu Structure

```
Menu
├── Screen (Category)
│   ├── Button (Item)
│   ├── Button (Item)
│   └── Button (Item Group)
│       ├── Option 1
│       ├── Option 2
│       └── Option 3
└── Screen (Category)
    └── ...
```

### Menu Management

**Location**: Menu > Menu Management

![Menu Management Page](screenshots/31-menu-management-page.jpg)

#### Creating a Menu
1. Click to create new menu or edit default
2. Add screens (categories)
3. Add buttons to screens
4. Assign items to buttons
5. Preview layout

![Menu Screen Editor](screenshots/32-menu-screen-editor.jpg)

![Add Buttons to Menu](screenshots/33-menu-add-buttons.jpg)

![Menu Preview](screenshots/34-menu-preview.jpg)

#### Menu Assignment
- Menus are assigned to POS configurations
- Changes sync to POS devices automatically

---

## Floor Plans & Table Management

### Purpose
- Visual representation of restaurant layout
- Essential for table service establishments
- Helps identify available tables
- Track occupied seats
- Handle billing

### Creating Floor Plans

**Location**: Configuration > Settings > Floor Plans

![Floor Plans Navigation](screenshots/35-floor-plans-navigation.jpg)

1. Click Add
2. Enter unique floor plan name
3. Click Save
4. Click Add Table
5. Configure:
   - Number of tables
   - Default covers per table
   - Table shape
6. Position and resize tables
7. Click Save

![Create Floor Plan](screenshots/36-create-floor-plan.jpg)

![Add Tables Dialog](screenshots/37-add-tables-dialog.jpg)

![Floor Plan Editor](screenshots/38-floor-plan-editor.jpg)

### Table Management
- Disable temporarily unavailable tables
- Edit table details (pencil icon)
- Modify position, number, size, covers

### Floor Plan Settings Tab
- Name
- Order profile link
- Background image
- Printer assignments

![Floor Plan Settings](screenshots/39-floor-plan-settings.jpg)

### Configuration Integration
- Disable table support for counter-service only
- Select different floor plans per POS device

---

## Order Processing

### Sales Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| Direct Sales | Immediate payment, no seating | Counter service, takeaway |
| Table Service | Seating, coursing, split checks | Dine-in restaurants |

### Direct Sales Flow

![Register - Direct Sales Mode](screenshots/40-register-direct-sales.jpg)

1. **Add Items**
   - Tap menu category
   - Tap item button
   - Use keypad for quantities

![Adding Items to Register](screenshots/41-add-items-register.jpg)

2. **Edit Order**
   - Tap Edit Order to expand item list
   - Select items to delete, reassign, or discount
   - Use Actions menu for order-level changes

![Edit Order Expanded](screenshots/42-edit-order-expanded.jpg)

![Actions Menu](screenshots/43-actions-menu.jpg)

3. **Payment**
   - Tap Pay or Quick Pay button
   - Select payment method
   - Confirm amount and tips
   - Complete transaction

![Quick Pay Buttons](screenshots/44-quick-pay-buttons.jpg)

### Table Service Flow

#### Starting a Table Service Order

**Method 1: Named Tab**
1. Tap Tab Name button
2. Enter customer/party name
3. Tap OK

![Tab Name Button](screenshots/45-tab-name-button.jpg)

**Method 2: Assign Table**
- From Floor Plan: Tap table to select
- From Register: Enter table number + tap Tables button

![Assign Table from Floor Plan](screenshots/46-assign-table-floor-plan.jpg)

![Table Service Register View](screenshots/47-table-service-register.jpg)

#### Coursing

Courses allow sequential preparation of items:
- Course 1 prepared first
- Fire subsequent courses when ready
- Course icons:
  - Orange flame = In-progress
  - Green check = Completed
  - No icon = Upcoming

![Coursing View](screenshots/48-coursing-view.jpg)

**Adding Items by Course**
1. Tap Add Course
2. Select course
3. Add items to course

**Adding Items by Seat**
1. Switch to "By Seat" view
2. Select seat
3. Select course
4. Add items

![By Seat View](screenshots/49-by-seat-view.jpg)

#### Firing Courses
- Tap Send to send initial order
- Tap Fire Course for subsequent courses
- Kitchen receives only newly fired items

![Fire Course Button](screenshots/51-fire-course-button.jpg)

#### Editing Table Service Orders

**Individual Items**
- Tap pencil icon next to item
- Change quantity, transfer seat, transfer course

**Multiple Items**
- Tap Edit Order
- Select items
- Choose action (Assign, Transfer, Discount, Remove)
- Tap Done

![Edit Order - Table Service](screenshots/50-edit-order-table-service.jpg)

**Entire Order**
- Tap Actions
- Options: Change name, Clear unsent, Refund all, Apply discount, Select order profile

### Order Profiles
- Pre-configured order settings
- Applied to orders for consistent handling
- Selected via Actions menu

---

## Payment Processing

### Lightspeed Payments Overview

![Lightspeed Payments Overview](screenshots/96-lightspeed-payments-overview.jpg)

Integrated payment processing features:
- Credit card payment management
- Reporting and refunding
- Daily deposit visibility
- Customer purchasing insights
- PCI compliance support (free via Viking Cloud)

### Application Process

**Location**: Financial Services (sidebar)

![Financial Services Application](screenshots/97-financial-services-application.jpg)

Required documentation (varies by region):
- Tax information
- Business owner identity verification
- Additional documentation per regional requirements

### Payment Configuration

**Location 1**: Configuration > Settings > Payment Methods

![Payment Methods Navigation](screenshots/53-payment-methods-navigation.jpg)

Settings available:
- **Authorization Timing**:
  - Standard: Authorized and captured immediately
  - Pre-authorized/Batch: Authorized instantly, captured next day (region-specific)
- Tipping enable/disable
- Tip percentage deduction for transaction fees
- Cash drawer settings
- Float settings
- Refund handling
- Payment QR codes
- Bar tabs
- Terminal receipts
- Surcharging (region-specific)

![Lightspeed Payments Settings](screenshots/54-lightspeed-payments-settings.jpg)

**Location 2**: Configuration > Configurations > [Configuration Name] > Payments

Settings available:
- Payment methods enabled
- Print final check after payment
- Signature capture
- Tipping options (with payment or afterward)

![Configuration Payments Tab](screenshots/55-configuration-payments-tab.jpg)

![Tipping Settings](screenshots/56-tipping-settings.jpg)

**Location 3**: Financial Services > Terminals

Settings available:
- Terminal language
- Default tipping options
- Pay at table (wireless payments)
- Standalone mode (process without POS)

![Financial Services Terminals](screenshots/57-financial-services-terminals.jpg)

![Terminal Settings](screenshots/58-terminal-settings.jpg)

### Taking Full Payments

**Quick Pay Method**
1. Add items to order
2. Tap Quick Pay button (Cash or Card)
3. Payment processes exact amount due

**Full Payment Screen Method**
1. Tap Pay button
2. View order summary
3. Select payment method
4. Enter received amount
5. Add tip (if applicable)
6. Tap Pay to complete

![Payment Screen Overview](screenshots/59-payment-screen-overview.jpg)

![Payment Methods Selection](screenshots/60-payment-methods.jpg)

![Tip Selection](screenshots/61-tip-selection.jpg)

![Payment Complete](screenshots/62-payment-complete.jpg)

### Splitting Checks

**Method 1: Register Screen Split Check**
1. Add all items to order
2. Tap Split Check
3. Add checks using + button
4. Move items between checks (tap item, tap "Move Here")
5. Split item costs (tap Split, select checks)
6. Tap Pay on each check
7. Process payments individually

![Split Check Button](screenshots/63-split-check-button.jpg)

![Split Check Screen](screenshots/64-split-check-screen.jpg)

![Move Items Between Checks](screenshots/65-move-items-between-checks.jpg)

![Split Item Dialog](screenshots/66-split-item-dialog.jpg)

**Method 2: Payment Screen (Table Service)**

*By Items Tab:*
1. View items with seat assignments
2. Tap items to add to current payment
3. Select payment method
4. Process payment
5. Repeat for remaining items

![Payment Items Tab](screenshots/67-payment-items-tab.jpg)

*By Covers Tab:*
1. Switch to Covers tab
2. Add/remove covers as needed
3. Tap paying covers to select
4. Process payment per cover

![Payment Covers Tab](screenshots/68-payment-covers-tab.jpg)

### Multiple Payments
- Continue processing payments until order total is covered
- Paid items removed from order summary after each payment

---

## Hardware Integration

### Essential Hardware

| Device | Purpose | Required |
|--------|---------|----------|
| iPad | POS application | Yes |
| Receipt Printer | Customer receipts | Yes |
| Kitchen Printer | Order tickets | Yes |
| Cash Drawer | Cash handling | Yes |
| Payment Terminal | Card processing | Yes |
| iPad Stand | Mounting | Yes |
| Router | Network connectivity | Yes |

### Optional Hardware
- Barcode Scanner (gift cards, packaged goods)
- Kitchen Display System (replaces kitchen printer)
- Order Display Screen (customer pickup status)

### Connection Types

| Type | Characteristics |
|------|-----------------|
| LAN (Ethernet) | Most reliable, can be shared between devices, requires cable |
| Bluetooth | Wireless, limited range, one device at a time |
| WiFi | Wireless, longer range than Bluetooth, can be shared |
| USB | Direct connection, one device at a time, requires cable |

**Recommendation**: Wired connections for stationary devices, especially in kitchen areas.

### Printer Setup

**Types:**
- **Thermal**: Fast, quiet, ideal for receipts (heat-sensitive paper)
- **Impact**: Heat-resistant, louder, ideal for kitchen (paper not heat-damaged)

**Physical Setup:**
1. Connect to power
2. Load paper (correct orientation)
3. Install ink ribbon (impact printers)
4. Connect to network/device based on connection type

**Back Office Configuration:**
Location: Hardware > Printers > Add Printer

Required information:
- Recognizable name
- Printer model (driver)
- IP address or MAC address (for LAN/WiFi)

**Self-Test Page (IP/MAC retrieval):**
- Turn printer off
- Hold power + feed buttons
- Release when printing begins

### Cash Drawer Setup

**Physical Setup:**
- Manual operation: Place on countertop
- Automatic operation: Connect to receipt printer
  - Large end of cable → drawer port
  - Small end → receipt printer
  - Set lock to vertical (unlocked) position

**Back Office Configuration:**
Location: Hardware > Cash Drawers > Create New Cash Drawer

Settings:
- Name (e.g., "POS 2 Cash Drawer")
- Counting: Enabled/Disabled/Mandatory
- Initial cash amount per shift
- Non-cash payment allocation
- Connected printer
- User group permissions
- Hardware permissions

**Note**: Cash drawers cannot be deleted once created.

### Payment Terminal Setup

**Requirements:**
- Use only terminals supplied by Lightspeed
- Third-party terminals may not be configured correctly

![Payment Terminal](screenshots/77-payment-terminal.jpg)

**Physical Setup:**
1. Charge if needed
2. Load paper roll (if applicable)
3. Connect to WiFi via terminal prompts
4. Complete model-specific setup

**Pairing:**
- Pair with Lightspeed Payments
- Assign to POS device or payment method
- Follow terminal-specific video playlist

---

## Cash Drawer Management

### Opening Cash Drawer

Timing: Beginning of sales period or after closing out

Process:
1. Sign into POS after opening sales period
2. If prompted, tap payment method to count
3. Select denomination
4. Enter count (number of notes/coins, not value)
5. Tap arrow to advance
6. Tap Confirm when done

![Count Denominations Screen](screenshots/70-count-denominations.jpg)

### Closing Cash Drawer

Requirements:
- All users except closing manager must be clocked out
- All orders completed
- Other POS cash drawers closed via settings menu

Process:
1. Sign in as shift manager
2. Tap Settings > Cash Drawer
3. Tap Open Drawer (to physically access)
4. Tap Close Drawer
5. Count each payment method
6. Tap Confirm Cash Amount
7. Print summary report (optional)

### Adding/Removing Cash

During open sales period:
1. Sign in with cash drawer permissions
2. Tap Settings > Cash Drawer
3. Tap Add Cash or Remove Cash
4. Enter amount and reason
5. Confirm action
6. Physically add/remove cash

![Add/Remove Cash](screenshots/72-add-remove-cash.jpg)

### Cash Drawer Reporting

**Automatic**: Prompted when closing drawer

**Manual**: Settings menu for previous reports

Report includes:
- Cash sales processed
- Add/remove reasons and totals
- Overage/shortage amount
- Related activity information

**Back Office Report**: Reports > Cash Drawer Report

---

## Sales Periods

### Opening a Sales Period

**Who**: Manager or user with correct permissions

![Clock In Screen](screenshots/23-clock-in-screen.jpg)

**Process:**
1. Open Lightspeed Restaurant app
2. Tap Clock In/Out from home screen
3. Find username under Clock In
4. Enter PIN or scan QR code
5. Complete Open Sales Period window:
   - Weather (optional)
   - Business level prediction (optional)
   - Custom note (optional)
6. Tap Open

![Open Sales Period Dialog](screenshots/24-open-sales-period-dialog.jpg)

### Closing a Sales Period

**Requirements:**
- All users (except closing manager) clocked out
- All orders completed
- All cash drawers on other POS devices closed

![Close Sales Period](screenshots/25-close-sales-period.jpg)

**Process:**
1. Log out to home screen
2. Tap Clock In/Out
3. Select username under Clock Out
4. Enter PIN or scan QR code
5. Declare tips (if prompted)
6. Count and close cash drawer
7. Print summary report (if prompted)

![Declare Tips Screen](screenshots/26-declare-tips-screen.jpg)

### Clock-in Only Users
- Can clock in without open sales period
- Used for back of house staff (chefs, etc.)

---

## Tax Configuration

### Tax Structure

```
Tax Profile
├── Tax Rule 1
│   ├── Tax (percentage)
│   └── Conditions (order profile, amounts, dates, etc.)
├── Tax Rule 2
│   └── ...
└── Applied Next Matching Rule (optional)
```

### Regional Behavior
- **North America**: Manual tax setup required
- **Other Regions**: Automatic configuration during account creation
- **Tax Inclusion**: Regional determination (included in price vs. added on)

### Creating Taxes

**Location**: Payment > Taxes (North America only)

Required fields:
- **Code**: Unique identifier
- **Description**: Receipt/reporting label
- **Rate**: Tax percentage

Optional fields:
- Accounting reference
- Category

![Add New Tax Form](screenshots/79-add-new-tax-form.jpg)

**Note**: Taxes cannot be deleted once created.

### Creating Tax Profiles

**Location**: Payment > Tax Profiles

![Tax Profiles Page](screenshots/80-tax-profiles-page.jpg)

Process:
1. Click New Tax Profile
2. Enter name and description
3. Select tax for first rule
4. Select order profile (All, Dine In, Takeout, etc.)
5. Add conditions (optional): receipt totals, dates, quantities
6. Enable "Applied Next Matching Rule" if multiple taxes apply
7. Add additional tax rules as needed
8. Save

![Tax Profile Rules](screenshots/81-tax-profile-rules.jpg)

### Assigning Tax Profiles to Accounting Groups

**Location**: Menu > Accounting Groups

![Accounting Groups Tax Assignment](screenshots/82-accounting-groups-tax.jpg)

Process:
1. Click accounting group name
2. Select tax profile
3. Save
4. Repeat for all accounting groups

---

## Multi-Location Management

### Business vs. Business Locations

| Type | Description | Use Case |
|------|-------------|----------|
| Business | Independent Lightspeed account | Different concepts/brands |
| Business Location | Linked under same business | Restaurant chains |

### Switching Locations

In Back Office:
1. Find dropdown in navigation sidebar
2. Select business location
3. Remains in same section, different location

![Location Switcher](screenshots/83-location-switcher.jpg)

### Feature Types

#### Global Features
Settings identical across all locations. Changes apply everywhere automatically.

Includes:
- Accounting groups
- Order profiles
- User groups

#### Shared Features

Some aspects same, others different per location:

| Feature | Shared | Location-Specific |
|---------|--------|-------------------|
| User Accounts | Available at all locations | Login restricted to home location unless granted |
| Customer Profiles | Shared across locations | Invoices, amounts, AR managed separately |
| Reporting | Location reports consolidate all | Individual location reports |

#### Local Features

Managed separately per location:
- Floor plans
- Tables
- Hardware
- POS devices

### Item Sharing

Items can be:
- **Local**: Only available in creation location
- **Shared**: Available all locations, different settings allowed
- **Global**: Same name and settings all locations

**Changing Status:**
Location: Menu > Item List

Process:
1. Check boxes next to items
2. Click Actions > Change Sharing Status
3. Select new status

Rules:
- Local → Shared only
- Shared → Global only
- Cannot revert to Local

### Menu Sharing

Menus are local by default.

![Menu Sharing](screenshots/85-menu-sharing.jpg)

**Sharing Process:**
1. Set items to Shared or Global first
2. Menu > Menu Management
3. Click three dots next to menu
4. Select "Share with Location"
5. Choose location
6. Confirm

---

## Reporting & Analytics

### POS Configuration Settings

**Location**: POS > Configuration

![Configuration Settings](screenshots/87-configuration-settings.jpg)

Configuration determines:
- Menu settings
- Production centers
- Order profiles
- Table support
- Order types
- Payment methods
- Refund permissions
- Discounts
- Loyalty cards
- Tipping
- Receipts
- Device settings
- Reporting options

![Order Management Settings](screenshots/88-order-management-settings.jpg)

![Payments in Configuration](screenshots/89-payments-configuration.jpg)

### Available Reports

**Cash Drawer Report**
- Location: Reports > Cash Drawer Report
- Content: POS activity affecting cash drawer

**Drawer Summary Report**
- Printed when closing cash drawer
- Cash sales to drawer
- Add/remove cash transactions
- Overage/shortage amounts

### Customer Insights
- Available with Lightspeed Payments
- Purchasing habits
- Trends analysis

---

## Support Resources

### Contact Departments

| Department | Purpose |
|------------|---------|
| Sales Representative | Account information, add-ons, hardware purchases |
| Launch Coordinator / Customer Success | Setup and onboarding assistance |
| Support Team | Technical guidance |

### Documentation
- K-Series Help Center
- Getting Started Guide (online)
- Feature documentation
- Hardware setup guides

---

## Appendix: POS Configurations

### Default Configuration
- Name: "Fixed POS"
- Based on business type selected at first login

### Configuration Categories

| Category | Settings Available |
|----------|-------------------|
| Basic Settings | Menu, production centers, order profiles |
| Order Management | Table support, order types, direct sales/orders |
| Order Tickets | Information displayed on tickets |
| Payments | Payment methods, refunds, tips |
| Discounts | Available discounts |
| Loyalty | Loyalty card settings |
| Receipts | Receipt formatting |
| Device | Hardware settings |
| Reporting | Report configuration |

### Multiple Configurations
- Create for different POS device needs
- Example: Bar area with unique drink menu
- Assign to specific devices

---

## Appendix: Clocking In/Out

### Clock In Process

1. Sales period must be open (except clock-in only users)
2. From home screen, tap Clock In/Out
3. Find username (or search)
4. Tap username
5. Enter PIN or scan QR code
6. Return to home screen (can now log in)

![Clock In Process](screenshots/90-clock-in-process.jpg)

### Clock Out Process

1. Log out to home screen
2. Tap Clock In/Out
3. Find username under Clock Out
4. Enter PIN or scan QR code
5. Transfer open orders if any (or void them)
6. Confirm clock out
7. Follow prompts for tips/cash drawer if applicable

![Clock Out Process](screenshots/91-clock-out-process.jpg)

![Transfer Orders Dialog](screenshots/92-transfer-orders-dialog.jpg)

### Auto Logout
- Configure in Back Office
- Options: After transaction, after preset time

---

*Specification generated from Lightspeed Restaurant K-Series official training videos.*
*Screenshots extracted from video frames at key UI moments.*
