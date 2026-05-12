// ============================================================
// Configuration
// ============================================================
const API_URL = "https://qu1ototlua.execute-api.eu-west-2.amazonaws.com/prod";

// ============================================================
// State
// ============================================================
let allChampions = [];
let currentPatch = "";
let currentPlayerName = "";
let currentPlayerTag = "";
let previousView = "search"; // Track where we came from

// ============================================================
// DOM Elements
// ============================================================
// Search view
const searchView = document.getElementById("search-view");
const playerNameInput = document.getElementById("player-name-input");
const playerTagInput = document.getElementById("player-tag-input");
const searchButton = document.getElementById("search-button");
const searchError = document.getElementById("search-error");
const championFilter = document.getElementById("champion-filter");
const championGrid = document.getElementById("champion-grid");
const loadingChampions = document.getElementById("loading-champions");
const patchBadge = document.getElementById("patch-badge");
const logo = document.getElementById("logo");

// Player view
const playerView = document.getElementById("player-view");
const playerBackButton = document.getElementById("player-back-button");
const playerLoading = document.getElementById("player-loading");
const playerContent = document.getElementById("player-content");
const playerError = document.getElementById("player-error");
const playerErrorMessage = document.getElementById("player-error-message");
const playerRetryButton = document.getElementById("player-retry-button");
const playerIcon = document.getElementById("player-icon");
const playerName = document.getElementById("player-name");
const playerLevel = document.getElementById("player-level");
const topChampions = document.getElementById("top-champions");
const matchHistory = document.getElementById("match-history");

// Champion view
const championView = document.getElementById("champion-view");
const championBackButton = document.getElementById("champion-back-button");
const championLoading = document.getElementById("champion-loading");
const championContent = document.getElementById("champion-content");
const championError = document.getElementById("champion-error");
const championErrorMessage = document.getElementById("champion-error-message");
const championRetryButton = document.getElementById("champion-retry-button");
const championIcon = document.getElementById("champion-icon");
const championName = document.getElementById("champion-name");
const championRoles = document.getElementById("champion-roles");
const personalStatsSection = document.getElementById("personal-stats-section");
const personalStats = document.getElementById("personal-stats");
const personalItemsSection = document.getElementById("personal-items-section");
const personalItems = document.getElementById("personal-items");
const championAbilities = document.getElementById("champion-abilities");
const championLore = document.getElementById("champion-lore");
const championSkins = document.getElementById("champion-skins");
const championMatchesSection = document.getElementById("champion-matches-section");
const championMatches = document.getElementById("champion-matches");

// ============================================================
// API Functions
// ============================================================
async function fetchChampions() {
    const response = await fetch(`${API_URL}/champions`);
    const data = await response.json();
    if (!data.success) throw new Error(data.error);
    return data.data;
}

async function fetchPlayerProfile(name, tag) {
    const response = await fetch(`${API_URL}/player/${encodeURIComponent(name)}/${encodeURIComponent(tag)}`);
    const data = await response.json();
    if (!data.success) throw new Error(data.error);
    return data.data;
}

async function fetchChampionPage(name, playerName = null, playerTag = null) {
    let url = `${API_URL}/champion/${encodeURIComponent(name)}`;
    if (playerName && playerTag) {
        url += `?player=${encodeURIComponent(playerName)}&tag=${encodeURIComponent(playerTag)}`;
    }
    const response = await fetch(url);
    const data = await response.json();
    if (!data.success) throw new Error(data.error);
    return data.data;
}

// ============================================================
// View Management
// ============================================================
function showView(viewName) {
    searchView.classList.add("hidden");
    playerView.classList.add("hidden");
    championView.classList.add("hidden");

    switch (viewName) {
        case "search":
            searchView.classList.remove("hidden");
            break;
        case "player":
            playerView.classList.remove("hidden");
            break;
        case "champion":
            championView.classList.remove("hidden");
            break;
    }
}

function showSearchView() {
    previousView = "search";
    showView("search");
}

// ============================================================
// Render: Champion Grid (Search View)
// ============================================================
function renderChampionGrid(champions) {
    championGrid.innerHTML = champions.map(champ => `
    <div class="champion-card" data-champion="${champ.name}" data-id="${champ.id}">
      <img src="${champ.imageUrl}" alt="${champ.name}" loading="lazy">
      <span>${champ.name}</span>
    </div>
  `).join("");

    championGrid.querySelectorAll(".champion-card").forEach(card => {
        card.addEventListener("click", () => {
            previousView = "search";
            showChampionView(card.dataset.id, card.dataset.id);
        });
    });
}

// ============================================================
// Render: Player Profile
// ============================================================
async function showPlayerView(name, tag) {
    currentPlayerName = name;
    currentPlayerTag = tag;
    previousView = "search";

    showView("player");
    playerLoading.classList.remove("hidden");
    playerContent.classList.add("hidden");
    playerError.classList.add("hidden");

    try {
        const profile = await fetchPlayerProfile(name, tag);

        playerIcon.src = profile.profileIconUrl;
        playerName.textContent = `${profile.gameName}#${profile.tagLine}`;
        playerLevel.textContent = `Level ${profile.summonerLevel}`;

        renderRankedStats(profile.rankedStats);
        renderMasteryChampions(profile.topMasteryChampions);
        renderTopChampions(profile.topChampions);
        renderMatchHistory(profile.recentMatches, matchHistory);

        playerLoading.classList.add("hidden");
        playerContent.classList.remove("hidden");
    } catch (error) {
        playerLoading.classList.add("hidden");
        playerError.classList.remove("hidden");
        playerErrorMessage.textContent = error.message || "Player not found";
    }
}

function renderTopChampions(champions) {
    if (!champions || champions.length === 0) {
        topChampions.innerHTML = '<p class="no-data">No champion data available</p>';
        return;
    }

    topChampions.innerHTML = champions.map(champ => {
        const winrateClass = champ.winRate >= 55 ? "winrate-good" : champ.winRate >= 45 ? "winrate-ok" : "winrate-bad";

        return `
      <div class="top-champion-card" data-champion="${champ.championName}" data-id="${champ.championId}">
        <img src="${champ.championIconUrl}" alt="${champ.championName}">
        <div class="top-champion-info">
          <div class="top-champion-name">${champ.championName}</div>
          <div class="top-champion-winrate ${winrateClass}">${champ.winRate}% WR</div>
          <div class="top-champion-stats">${champ.gamesPlayed} games · ${champ.kdaString}</div>
        </div>
      </div>
    `;
    }).join("");

    topChampions.querySelectorAll(".top-champion-card").forEach(card => {
        card.addEventListener("click", () => {
            previousView = "player";
            showChampionView(card.dataset.id, card.dataset.id, currentPlayerName, currentPlayerTag);
        });
    });
}

function renderMatchHistory(matches, container) {
    if (!matches || matches.length === 0) {
        container.innerHTML = '<p class="no-data">No recent matches found</p>';
        return;
    }

    container.innerHTML = matches.map(match => `
    <div class="match-row ${match.win ? 'match-win' : 'match-loss'}" data-champion="${match.championName}" data-id="${match.championId}">
      <span class="match-result ${match.win ? 'win' : 'loss'}">${match.win ? 'WIN' : 'LOSS'}</span>
      <img class="match-champion-icon" src="${match.championIconUrl}" alt="${match.championName}">
      <div class="match-details">
        <div class="match-champion-name">${match.championName}</div>
        <div class="match-kda">${match.kdaString} · ${match.creepScore} CS</div>
      </div>
      <div class="match-items">
        ${match.itemIconUrls.map(url => `
          <img class="match-item-icon" src="${url}" alt="">
        `).join("")}
      </div>
      <div class="match-meta">
        <span class="match-duration">${match.gameDurationString}</span>
        <span class="match-time-ago">${match.timeAgo}</span>
      </div>
    </div>
  `).join("");

    container.querySelectorAll(".match-row").forEach(row => {
        row.addEventListener("click", () => {
            previousView = "player";
            showChampionView(row.dataset.id, row.dataset.id, currentPlayerName, currentPlayerTag);
        });
    });
}

// ============================================================
// Render: Champion Page
// ============================================================
async function showChampionView(name, id, playerNameParam = null, playerTagParam = null) {
    showView("champion");
    championLoading.classList.remove("hidden");
    championContent.classList.add("hidden");
    championError.classList.add("hidden");

    // Hide personal sections initially
    personalStatsSection.classList.add("hidden");
    personalItemsSection.classList.add("hidden");
    championMatchesSection.classList.add("hidden");

    try {
        // Load champion info first (fast — no player data)
        const data = await fetchChampionPage(name);

        championIcon.src = `https://ddragon.leagueoflegends.com/cdn/${data.patch}/img/champion/${data.championId}.png`;
        championName.textContent = `${data.championName} — ${data.title}`;
        championRoles.textContent = data.roles.join(", ");

        renderAbilities(data.abilities);
        renderLore(data.lore);
        renderSkins(data.skins);

        // Show champion content immediately
        championLoading.classList.add("hidden");
        championContent.classList.remove("hidden");

        // If we have player context, load personal stats separately
        if (playerNameParam && playerTagParam) {
            loadPersonalStats(data.championName, data.championId, playerNameParam, playerTagParam);
        }

    } catch (error) {
        championLoading.classList.add("hidden");
        championError.classList.remove("hidden");
        championErrorMessage.textContent = error.message || "Failed to load champion data";
    }
}

async function loadPersonalStats(championName, championId, playerNameParam, playerTagParam) {
    // Show sections with loading state
    personalStatsSection.classList.remove("hidden");
    personalStats.innerHTML = `
    <div class="stats-loading">
      <div class="spinner"></div>
      <p>Loading your stats...</p>
    </div>
  `;

    personalItemsSection.classList.remove("hidden");
    personalItems.innerHTML = '';

    championMatchesSection.classList.remove("hidden");
    championMatches.innerHTML = '';

    try {
        const data = await fetchChampionPage(championId, playerNameParam, playerTagParam);

        if (data.personalStats) {
            renderPersonalStats(data.personalStats);
            renderPersonalItems(data.personalStats.mostBuiltItems);
            renderMatchHistory(data.personalStats.matchHistory, championMatches);
        } else {
            personalStats.innerHTML = '<p class="no-data">No personal stats available for this champion</p>';
            personalItemsSection.classList.add("hidden");
            championMatchesSection.classList.add("hidden");
        }
    } catch (error) {
        personalStats.innerHTML = `
      <div class="stats-error">
        <p>Could not load personal stats</p>
        <button class="retry-builds-btn" onclick="loadPersonalStats('${championName}', '${championId}', '${playerNameParam}', '${playerTagParam}')">Try again</button>
      </div>
    `;
        personalItemsSection.classList.add("hidden");
        championMatchesSection.classList.add("hidden");
    }
}

function renderPersonalStats(stats) {
    const winrateClass = stats.winRate >= 55 ? "winrate-good" : stats.winRate >= 45 ? "winrate-ok" : "winrate-bad";

    personalStats.innerHTML = `
    <div class="stat-card">
      <div class="stat-card-value ${winrateClass}">${stats.winRate}%</div>
      <div class="stat-card-label">Win Rate</div>
    </div>
    <div class="stat-card">
      <div class="stat-card-value">${stats.gamesPlayed}</div>
      <div class="stat-card-label">Games (${stats.wins}W ${stats.losses}L)</div>
    </div>
    <div class="stat-card">
      <div class="stat-card-value">${stats.avgKda}</div>
      <div class="stat-card-label">Avg KDA</div>
    </div>
    <div class="stat-card">
      <div class="stat-card-value">${stats.kdaString}</div>
      <div class="stat-card-label">Avg K/D/A</div>
    </div>
    <div class="stat-card">
      <div class="stat-card-value">${stats.avgCreepScore}</div>
      <div class="stat-card-label">Avg CS</div>
    </div>
    <div class="stat-card">
      <div class="stat-card-value">${stats.masteryPointsFormatted}</div>
      <div class="stat-card-label">Mastery (Level ${stats.masteryLevel})</div>
    </div>
    <p class="stats-context">Based on your last ${stats.gamesAnalysed || stats.gamesPlayed} matches analysed</p>
  `;
}

function renderPersonalItems(items) {
    if (!items || items.length === 0) {
        personalItems.innerHTML = '<p class="no-data">No item data available</p>';
        return;
    }

    personalItems.innerHTML = items.map(item => `
    <div class="item-with-name">
      <img class="item-icon" src="${item.imageUrl}" alt="${item.itemName}" title="${item.itemName}">
      <span class="item-name">${item.itemName}</span>
      <span class="item-times-built">${item.timesBuilt}x built</span>
    </div>
  `).join("");
}

function renderAbilities(abilities) {
    if (!abilities) {
        championAbilities.innerHTML = '<p class="no-data">No ability data available</p>';
        return;
    }

    const abilityList = [
        { key: "Passive", ...abilities.passive },
        { key: "Q", ...abilities.q },
        { key: "W", ...abilities.w },
        { key: "E", ...abilities.e },
        { key: "R", ...abilities.r }
    ];

    championAbilities.innerHTML = abilityList.map(ability => `
    <div class="ability-row">
      <img class="ability-icon" src="${ability.imageUrl}" alt="${ability.name}">
      <div class="ability-info">
        <div class="ability-name"><span class="ability-key">${ability.key}</span> ${ability.name}</div>
        <div class="ability-description">${ability.description}</div>
      </div>
    </div>
  `).join("");
}

function renderLore(lore) {
    championLore.innerHTML = `<p class="lore-text">${lore || "No lore available."}</p>`;
}

function renderSkins(skins) {
    if (!skins || skins.length === 0) {
        championSkins.innerHTML = '<p class="no-data">No skin data available</p>';
        return;
    }

    championSkins.innerHTML = `
    <div class="skins-grid">
      ${skins.map(skin => `
        <div class="skin-card">
          <img class="skin-image" src="${skin.loadingUrl}" alt="${skin.name}" loading="lazy">
          <div class="skin-name">${skin.name}</div>
          ${skin.hasChromas ? '<div class="skin-chromas-badge">Chromas available</div>' : ''}
        </div>
      `).join("")}
    </div>
  `;
}

function renderMasteryChampions(champions) {
    const masteryChampions = document.getElementById("mastery-champions");

    if (!champions || champions.length === 0) {
        masteryChampions.innerHTML = '<p class="no-data">No mastery data available</p>';
        return;
    }

    // Show top 5
    const top5 = champions.slice(0, 5);

    masteryChampions.innerHTML = top5.map(champ => `
    <div class="mastery-card" data-champion="${champ.championName}" data-id="${champ.championId}">
      <img class="mastery-card-icon" src="${champ.championIconUrl}" alt="${champ.championName}">
      <div class="mastery-card-name">${champ.championName}</div>
      <div class="mastery-card-level">Mastery ${champ.masteryLevel}</div>
      <div class="mastery-card-points">${champ.masteryPointsFormatted} pts</div>
      ${champ.gamesPlayed > 0
            ? `<div class="mastery-card-stats">${champ.winRate}% WR · ${champ.gamesPlayed} recent games</div>`
            : '<div class="mastery-card-stats">No recent games</div>'
        }
    </div>
  `).join("");

    masteryChampions.querySelectorAll(".mastery-card").forEach(card => {
        card.addEventListener("click", () => {
            previousView = "player";
            showChampionView(card.dataset.id, card.dataset.id, currentPlayerName, currentPlayerTag);
        });
    });
}

function renderRankedStats(rankedStats) {
    const rankedContainer = document.getElementById("ranked-stats");

    if (!rankedStats || rankedStats.length === 0) {
        rankedContainer.innerHTML = '<div class="ranked-card"><span class="ranked-queue">Unranked</span></div>';
        return;
    }

    rankedContainer.innerHTML = rankedStats.map(rank => `
    <div class="ranked-card">
      <img class="ranked-icon" src="${rank.tierIconUrl}" alt="${rank.tier}" onerror="this.style.display='none'">
      <div class="ranked-info">
        <span class="ranked-queue">${rank.queueName}</span>
        <span class="ranked-tier">${rank.tier} ${rank.rank}</span>
        <span class="ranked-lp">${rank.leaguePoints} LP</span>
        <span class="ranked-record">${rank.wins}W ${rank.losses}L (${rank.winRate}% WR)</span>
      </div>
    </div>
  `).join("");
}

// ============================================================
// Event Handlers
// ============================================================
logo.addEventListener("click", showSearchView);

// Player search
searchButton.addEventListener("click", () => {
    const name = playerNameInput.value.trim();
    const tag = playerTagInput.value.trim();

    if (!name || !tag) {
        searchError.textContent = "Please enter both a game name and tag";
        searchError.classList.remove("hidden");
        return;
    }

    searchError.classList.add("hidden");
    showPlayerView(name, tag);
});

// Enter key on search inputs
playerNameInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") searchButton.click();
});

playerTagInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") searchButton.click();
});

// Champion filter
championFilter.addEventListener("input", (e) => {
    const query = e.target.value.toLowerCase();
    const filtered = allChampions.filter(champ =>
        champ.name.toLowerCase().includes(query)
    );
    renderChampionGrid(filtered);
});

// Back buttons
playerBackButton.addEventListener("click", showSearchView);

championBackButton.addEventListener("click", () => {
    if (previousView === "player") {
        showView("player");
    } else {
        showSearchView();
    }
});

// Retry buttons
playerRetryButton.addEventListener("click", () => {
    showPlayerView(currentPlayerName, currentPlayerTag);
});

championRetryButton.addEventListener("click", () => {
    // Retry with same params
    showView("champion");
});

// ============================================================
// Initialise
// ============================================================
async function init() {
    try {
        const data = await fetchChampions();
        allChampions = data.champions;
        currentPatch = data.patch;

        patchBadge.textContent = `Patch ${currentPatch}`;

        loadingChampions.classList.add("hidden");
        renderChampionGrid(allChampions);
    } catch (error) {
        loadingChampions.innerHTML = `
      <p class="error-message">Failed to load champions</p>
      <button class="retry-button" onclick="init()">Try again</button>
    `;
    }
}

init();
