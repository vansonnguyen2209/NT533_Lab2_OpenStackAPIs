using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace OpenStack
{
    public partial class OpenStack : Form
    {
        string token;
        private List<Tuple<string, string>> createdNetworks = new List<Tuple<string, string>>();

        List<Tuple<string, string, string>> createdSubnets = new List<Tuple<string, string, string>>();

        private List<Tuple<string, string>> createdPorts = new List<Tuple<string, string>>();
        public OpenStack()
        {
            InitializeComponent();
        }
       
        private async void login_button_Click(object sender, EventArgs e)
        {
            if (username_textbox.Text.Trim() == "" || password_textbox.Text.Trim() == "" || domain_textbox.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin đăng nhập!");return;
            }
            string username = username_textbox.Text.Trim();
            string password = password_textbox.Text.Trim();
            string domain = domain_textbox.Text.Trim();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = "https://cloud-identity.uitiot.vn/v3/auth/tokens";

                    var authData = new
                    {
                        auth = new
                        {
                            identity = new
                            {
                                methods = new[] { "password" },
                                password = new
                                {
                                    user = new
                                    {
                                        name = username,
                                        password = password,
                                        domain = new { name = domain}
                                    }
                                }
                            },
                            scope = new
                            {
                                project = new
                                {
                                    name = "NT533.P21",
                                    domain = new { id = "default" }
                                }
                            }
                        }
                    };
                    string json = JsonSerializer.Serialize(authData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string result = await response.Content.ReadAsStringAsync();
                    if(!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Đăng nhập thất bại! Vui lòng kiểm tra lại thông tin đăng nhập.");
                        return;
                    }
                    token = response.Headers.GetValues("X-Subject-Token").FirstOrDefault();
                    Log("Đăng nhập thành công!");

                    resource_groupbox.Visible = true;
                    network_button.Visible = true;
                    port_button.Visible = true;
                    instance_button.Visible = true;
                    log_groupbox.Visible = true;
                    log_richtextbox.Visible = true;
                    signin_panel.Visible = false;

                }
                if (!string.IsNullOrEmpty(token))
                {
                    await LoadOpenStackComboBoxes(); // load images, flavors, keypairs lên ComboBox
                }
            }
            catch (Exception ex)
            {
                Log("Lỗi: " + ex.Message, true);
            }
        }

        private void network_button_Click(object sender, EventArgs e)
        {
            network_button.Enabled = false;
            port_button.Enabled = true;
            instance_button.Enabled = true;
            createNetwork_groupbox.Visible = true;
            createPort_groupbox.Visible = false;
            createInstance_groupbox.Visible = false;
        }

        private void port_button_Click(object sender, EventArgs e)
        {
            network_button.Enabled = true;
            port_button.Enabled = false;
            instance_button.Enabled = true;
            createNetwork_groupbox.Visible = false;
            createPort_groupbox.Visible = true;
            createInstance_groupbox.Visible = false;
        }
        
        private void instance_button_Click(object sender, EventArgs e)
        {
            network_button.Enabled = true;
            port_button.Enabled = true;
            instance_button.Enabled = false;
            createNetwork_groupbox.Visible = false;
            createPort_groupbox.Visible = false;
            createInstance_groupbox.Visible = true;
        }

        private async void createNetwork_button_Click(object sender, EventArgs e)
        {
            string networkName = networkName_textbox.Text.Trim();
            string subnetName = subnetName_textbox.Text.Trim();
            string cidr = networkAddress_textbox.Text.Trim();
            string ipSelected = ipVersion_combobox.SelectedItem?.ToString();

            // ---- Kiểm tra input ----
            if (string.IsNullOrWhiteSpace(networkName))
            {
                MessageBox.Show("Vui lòng nhập tên network.");return;
            }
            if (string.IsNullOrWhiteSpace(subnetName))
            {
                MessageBox.Show("Vui lòng nhập tên subnet."); return;
            }
            if (string.IsNullOrWhiteSpace(cidr) || !cidr.Contains("/"))
            {
                MessageBox.Show("Vui lòng nhập CIDR hợp lệ (ví dụ: 192.168.1.0/24).");
                return;
            }
            if (string.IsNullOrWhiteSpace(ipSelected))
            {
                MessageBox.Show("Vui lòng chọn IP version (IPv4 hoặc IPv6).");
                return;
            }
            int ipVersion = ipSelected == "IPv4" ? 4 : 6;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Auth-Token", token);

                    // ---- TẠO NETWORK ----
                    string urlNetwork = "https://cloud-network.uitiot.vn/v2.0/networks";
                    var createNetwork = new { network = new { name = networkName, admin_state_up = true } };
                    string jsonNetwork = JsonSerializer.Serialize(createNetwork);
                    var contentNetwork = new StringContent(jsonNetwork, Encoding.UTF8, "application/json");

                    HttpResponseMessage responseNetwork = await client.PostAsync(urlNetwork, contentNetwork);
                    string resultNetwork = await responseNetwork.Content.ReadAsStringAsync();

                    if (!responseNetwork.IsSuccessStatusCode)
                    {
                        Log("Tạo network thất bại!", true);
                        return;
                    }

                    string networkID;
                    using (JsonDocument doc = JsonDocument.Parse(resultNetwork))
                    {
                        networkID = doc.RootElement.GetProperty("network").GetProperty("id").GetString();
                    }

                    // Thêm vào danh sách network và cập nhật combobox
                    createdNetworks.Add(Tuple.Create(networkName, networkID));
                    networkName_combobox.DataSource = null;
                    networkName_combobox.DataSource = createdNetworks;
                    networkName_combobox.DisplayMember = "Item1";
                    networkName_combobox.ValueMember = "Item2";
                    networkName_combobox.SelectedIndex = -1; // không chọn sẵn
                    Log($"Tạo network thành công! Network: {networkName}");

                    // ---- TẠO SUBNET ----
                    string urlSubnet = "https://cloud-network.uitiot.vn/v2.0/subnets";
                    var createSubnet = new
                    {
                        subnet = new
                        {
                            name = subnetName,
                            network_id = networkID,
                            cidr = cidr,
                            ip_version = ipVersion
                        }
                    };
                    string jsonSubnet = JsonSerializer.Serialize(createSubnet);
                    var contentSubnet = new StringContent(jsonSubnet, Encoding.UTF8, "application/json");

                    HttpResponseMessage responseSubnet = await client.PostAsync(urlSubnet, contentSubnet);
                    string resultSubnet = await responseSubnet.Content.ReadAsStringAsync();

                    if (!responseSubnet.IsSuccessStatusCode)
                    {
                        Log("Tạo subnet thất bại!", true);
                        return;
                    }
        
                    string subnetID;
                    using (JsonDocument doc1 = JsonDocument.Parse(resultSubnet))
                    {
                        subnetID = doc1.RootElement.GetProperty("subnet").GetProperty("id").GetString();
                    }

                    // Thêm vào danh sách subnet và cập nhật combobox
                    createdSubnets.Add(Tuple.Create(subnetName, subnetID, networkID));
                    subnetName_combobox.DataSource = null;
                    subnetName_combobox.DataSource = createdSubnets;
                    subnetName_combobox.DisplayMember = "Item1";
                    subnetName_combobox.ValueMember = "Item2";
                    subnetName_combobox.SelectedIndex = -1;
                    Log($"Tạo subnet thành công! Subnet: {subnetName}");

                    networkName_textbox.Text = "";
                    subnetName_textbox.Text = "";
                    networkAddress_textbox.Text = "";
                    ipVersion_combobox.SelectedIndex = -1;

                }
            }
            catch (Exception ex)
            {
                Log("Lỗi: " + ex.Message, true);
            }
        }

         private void networkName_combobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string networkID = networkName_combobox.SelectedValue?.ToString();

            if (!string.IsNullOrEmpty(networkID))
            {
                var filtered = createdSubnets.Where(s => s.Item3 == networkID).ToList();

                subnetName_combobox.DataSource = null;
                subnetName_combobox.DataSource = filtered;
                subnetName_combobox.DisplayMember = "Item1"; // hiển thị tên subnet
                subnetName_combobox.ValueMember = "Item2";   // giá trị là ID subnet
                subnetName_combobox.SelectedIndex = -1;      // không chọn sẵn
            }
            else subnetName_combobox.DataSource = null;
        }

        private async void createPort_button_Click(object sender, EventArgs e)
        {
            if (networkName_combobox.SelectedValue.ToString() == null || subnetName_combobox.SelectedValue.ToString() == null)
            {
                MessageBox.Show("Vui lòng chọn network và subnet.");return;
            }
   
            if (string.IsNullOrWhiteSpace(portName_textbox.Text.Trim()))
            {
                MessageBox.Show("Vui lòng nhập tên port.");
                return;
            }

            string portName = portName_textbox.Text.Trim();
            string networkID = networkName_combobox.SelectedValue.ToString();
            string subnetID = subnetName_combobox.SelectedValue.ToString();
            string fixedIP = ipAddress_textbox.Text.Trim();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Auth-Token", token);

                    string urlPort = "https://cloud-network.uitiot.vn/v2.0/ports";
                    var portData = new
                    {
                        port = new
                        {
                            name = portName,
                            network_id = networkID,
                            admin_state_up = true,
                            fixed_ips = new[]
                            {
                                new
                                {
                                    subnet_id = subnetID,
                                    ip_address = string.IsNullOrWhiteSpace(fixedIP) ? null : fixedIP
                                }
                            }
                        }
                    };

                    string jsonPort = JsonSerializer.Serialize(portData);
                    var contentPort = new StringContent(jsonPort, Encoding.UTF8, "application/json");

                    HttpResponseMessage responsePort = await client.PostAsync(urlPort, contentPort);
                    string resultPort = await responsePort.Content.ReadAsStringAsync();

                    if (!responsePort.IsSuccessStatusCode)
                    {
                        Log("Tạo port thất bại!", true);
                        return;
                    }

                    string portID;
                    using (JsonDocument doc = JsonDocument.Parse(resultPort))
                    {
                        portID = doc.RootElement.GetProperty("port").GetProperty("id").GetString();
                    }

                    // Thêm vào danh sách network và cập nhật combobox
                    createdPorts.Add(Tuple.Create(portName, portID));
                    portName_checklistbox.DataSource = null;
                    portName_checklistbox.DataSource = createdPorts;
                    portName_checklistbox.DisplayMember = "Item1";
                    portName_checklistbox.ValueMember = "Item2";
                    portName_checklistbox.SelectedIndex = -1; // không chọn sẵn
                    Log($"Tạo port thành công! Port: {portName}");

                    networkName_combobox.SelectedIndex = -1;
                    subnetName_combobox.SelectedIndex = -1;
                    portName_textbox.Text = "";
                    ipAddress_textbox.Text = "";
                }
            }
            catch (Exception ex)
            {
                Log("Lỗi khi tạo port: " + ex.Message, true);
            }
        }

      private async void createInstance_button_Click(object sender, EventArgs e)
{
    // 🟩 Kiểm tra tên instance
    string instanceName = instanceName_textbox.Text.Trim();
    if (string.IsNullOrWhiteSpace(instanceName))
    {
        MessageBox.Show("Vui lòng nhập tên instance.");
        return;
    }

    // 🟩 Kiểm tra image
    if (image_combobox.SelectedValue == null)
    {
        MessageBox.Show("Vui lòng chọn image.");
        return;
    }

    // 🟩 Kiểm tra flavor
    if (flavor_combobox.SelectedValue == null)
    {
        MessageBox.Show("Vui lòng chọn flavor.");
        return;
    }

    // 🟩 Kiểm tra security group
    if (securitygroup_combobox.SelectedValue == null)
    {
        MessageBox.Show("Vui lòng chọn security group.");
        return;
    }

    // 🟩 Keypair có thể không chọn
    string keypairName = null;
    if (keypair_combobox.SelectedValue != null && !string.IsNullOrWhiteSpace(keypair_combobox.SelectedValue.ToString()))
    {
        keypairName = keypair_combobox.SelectedValue.ToString();
    }

    // 🟩 Kiểm tra danh sách port
    if (portName_checklistbox.CheckedItems.Count == 0)
    {
        MessageBox.Show("Vui lòng chọn ít nhất một port.");
        return;
    }

    string imageId = image_combobox.SelectedValue.ToString();
    string flavorId = flavor_combobox.SelectedValue.ToString();
    string securityGroupName = securitygroup_combobox.SelectedValue.ToString();

    // 🟩 Lấy danh sách port ID được chọn
    List<string> portIds = new List<string>();
    foreach (var item in portName_checklistbox.CheckedItems)
    {
        if (item is Tuple<string, string> t)
            portIds.Add(t.Item2); // lấy portID
    }

    try
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("X-Auth-Token", token);

            string urlInstance = "https://cloud-compute.uitiot.vn/v2.1/e5546ae00fff4785910a067269b5725a/servers";

            // 🟩 Dữ liệu JSON gửi đi
            var instanceData = new
            {
                server = new
                {
                    name = instanceName,
                    imageRef = imageId,
                    flavorRef = flavorId,
                    key_name = keypairName,
                    security_groups = new[]
                    {
                        new { name = securityGroupName }
                    },
                    networks = portIds.Select(p => new { port = p }).ToArray(),
                    user_data = "I2Nsb3VkLWNvbmZpZwpjaHBhc3N3ZDoKIGxpc3Q6IHwKICAgcm9vdDpyb290CmV4cGlyZTogRmFsc2UKc3NoX3B3YXV0aDogVHJ1ZQ==",
                    min_count = 1,
                    max_count = 1
                }
            };

            // 🟩 Serialize thành JSON
            string jsonBody = JsonSerializer.Serialize(instanceData);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // 🟩 Gửi POST request
            HttpResponseMessage response = await client.PostAsync(urlInstance, content);

            if (response.IsSuccessStatusCode)
            {
                Log($"✅ Tạo instance thành công: {instanceName}");

                // 🟩 Reset giao diện
                instanceName_textbox.Text = "";
                image_combobox.SelectedIndex = -1;
                flavor_combobox.SelectedIndex = -1;
                keypair_combobox.SelectedIndex = -1;

                // Giữ lại security group = "default" nếu có trong danh sách
                int defaultIndex = -1;
                for (int i = 0; i < securitygroup_combobox.Items.Count; i++)
                {
                    var item = securitygroup_combobox.Items[i];
                    if (item.ToString().ToLower().Contains("default"))
                    {
                        defaultIndex = i;
                        break;
                    }
                }
                securitygroup_combobox.SelectedIndex = defaultIndex;

                // Bỏ chọn các port
                for (int i = 0; i < portName_checklistbox.Items.Count; i++)
                    portName_checklistbox.SetItemChecked(i, false);
            }
            else
            {
                Log("❌ Tạo instance thất bại.", true);
                MessageBox.Show("Tạo instance thất bại!");
            }
        }
    }
    catch (Exception ex)
    {
        Log("⚠️ Lỗi khi tạo instance: " + ex.Message, true);
    }
}

        private async Task LoadOpenStackComboBoxes()
        {
            var images = await GetOpenStackV2Resources("https://cloud-compute.uitiot.vn/v2/images", "images");
            image_combobox.DataSource = images;
            image_combobox.DisplayMember = "Item1";
            image_combobox.ValueMember = "Item2";
            image_combobox.SelectedIndex = -1; // không chọn sẵn

            // Flavors
            var flavors = await GetOpenStackV2Resources("https://cloud-compute.uitiot.vn/v2/flavors", "flavors");
            flavor_combobox.DataSource = flavors;
            flavor_combobox.DisplayMember = "Item1";
            flavor_combobox.ValueMember = "Item2";
            flavor_combobox.SelectedIndex = -1; // không chọn sẵn

            // KeyPairs
            var keypairs = await GetOpenStackV2Resources("https://cloud-compute.uitiot.vn/v2/os-keypairs", "keypairs");
            keypair_combobox.DataSource = keypairs;
            keypair_combobox.DisplayMember = "Item1";
            keypair_combobox.ValueMember = "Item2";
            keypair_combobox.SelectedIndex = -1; // không chọn sẵn

            // Security Groups
            var sgs = await GetOpenStackV2Resources("https://cloud-network.uitiot.vn/v2.0/security-groups", "securitygroups");
            securitygroup_combobox.DataSource = sgs;
            securitygroup_combobox.DisplayMember = "Item1";
            securitygroup_combobox.ValueMember = "Item2";

            // Chọn default Security Group nếu có
            var defaultIndex = sgs.FindIndex(s => s.Item1 == "default");
            if (defaultIndex >= 0)
                securitygroup_combobox.SelectedIndex = defaultIndex;
        }
        private async Task<List<Tuple<string, string>>> GetOpenStackV2Resources(string url, string type)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Auth-Token", token);

                    HttpResponseMessage response = await client.GetAsync(url);
                    string result = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Lấy {type} thất bại: {response.StatusCode}\n{result}");
                        return new List<Tuple<string, string>>();
                    }

                    var list = new List<Tuple<string, string>>();
                    using (JsonDocument doc = JsonDocument.Parse(result))
                    {
                        var root = doc.RootElement;
                        switch (type)
                        {
                            case "images":
                                if (root.TryGetProperty("images", out JsonElement images))
                                {
                                    foreach (var img in images.EnumerateArray())
                                    {
                                        string name = img.GetProperty("name").GetString();
                                        string id = img.GetProperty("id").GetString();
                                        list.Add(Tuple.Create(name, id));
                                    }
                                }
                                break;
                            case "flavors":
                                if (root.TryGetProperty("flavors", out JsonElement flavors))
                                {
                                    foreach (var f in flavors.EnumerateArray())
                                    {
                                        string name = f.GetProperty("name").GetString();
                                        string id = f.GetProperty("id").GetString();
                                        list.Add(Tuple.Create(name, id));
                                    }
                                }
                                break;
                            case "keypairs":
                                if (root.TryGetProperty("keypairs", out JsonElement keypairs))
                                {
                                    foreach (var k in keypairs.EnumerateArray())
                                    {
                                        string name = k.GetProperty("keypair").GetProperty("name").GetString();
                                        list.Add(Tuple.Create(name, name));
                                    }
                                }
                                break;
                            case "securitygroups":
                                if (root.TryGetProperty("security_groups", out JsonElement sgs))
                                    foreach (var sg in sgs.EnumerateArray())
                                        list.Add(Tuple.Create(sg.GetProperty("name").GetString(), sg.GetProperty("id").GetString()));
                                break;
                        }
                    }
                    return list;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lấy " + type + ": " + ex.Message);
                return new List<Tuple<string, string>>();
            }
        }



        private void username_label_Click(object sender, EventArgs e)
        {

        }

        private void username_textbox_TextChanged(object sender, EventArgs e)
        {

        }

        private void createPort_groupbox_Enter(object sender, EventArgs e)
        {

        }

        private void createInstance_groupbox_Enter(object sender, EventArgs e)
        {

        }

        private void networkName_textbox_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void createNetwork_groupbox_Enter(object sender, EventArgs e)
        {

        }

        private void instanceName_textbox_TextChanged(object sender, EventArgs e)
        {

        }

        private void signin_panel_Paint(object sender, PaintEventArgs e)
        {

        }
        private void Log(string message, bool isError = false)
        {
            if (log_richtextbox.InvokeRequired)
            {
                log_richtextbox.Invoke(new Action(() => {
                    log_richtextbox.SelectionColor = isError ? Color.Red : Color.Black;
                    log_richtextbox.AppendText(message + "\n");
                    log_richtextbox.ScrollToCaret();
                }));
            }
            else
            {
                log_richtextbox.SelectionColor = isError ? Color.Red : Color.Black;
                log_richtextbox.AppendText(message + "\n");
                log_richtextbox.ScrollToCaret();
            }
        }

        private void subnetName_combobox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void OpenStack_Load(object sender, EventArgs e)
        {

        }


    }
}

