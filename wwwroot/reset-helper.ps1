
# This script resets the admin password using the app's own hasher via API
# We'll call the app's change password endpoint

# First: figure out the current admin email from DB
$email = "goo594334@gmail.com"
Write-Host "Admin email in DB: $email"
