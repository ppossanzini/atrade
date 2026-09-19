<script lang="ts" src="./ProposalQueue.ts"></script>

<template>
  <el-table
    v-loading="isLoading"
    :data="proposals"
    :empty-text="t('market.noProposals')"
    size="small"
    class="proposal-queue"
  >
    <el-table-column :label="t('market.time')" width="84">
      <template #default="scope">{{ formatTime(scope.row.proposedAtUtc) }}</template>
    </el-table-column>

    <el-table-column :label="t('market.proposal')" min-width="190">
      <template #default="scope">
        <div class="proposal-queue__identity">
          <strong>{{ scope.row.basketName }} · v{{ scope.row.versionNumber }}</strong>
          <small>{{ shortId(scope.row.proposalId) }}</small>
        </div>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.action')" width="104">
      <template #default="scope">
        <el-tag :type="actionTagType(scope.row.action)" size="small" effect="plain">
          {{ t(`proposalAction.${scope.row.action}`) }}
        </el-tag>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.confidence')" width="118">
      <template #default="scope">
        <el-progress :percentage="scope.row.confidence" :stroke-width="7" />
      </template>
    </el-table-column>

    <el-table-column :label="t('market.risk')" width="96" align="right">
      <template #default="scope">
        <strong>{{ scope.row.expectedRiskPercent.toFixed(2) }} %</strong>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.gate')" width="112">
      <template #default="scope">
        <el-tag :type="gateTagType(scope.row.gate)" size="small">
          {{ t(`riskVerdict.${scope.row.gate}`) }}
        </el-tag>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.status')" width="136">
      <template #default="scope">
        <el-tag :type="statusTagType(scope.row.status)" size="small" effect="plain">
          {{ t(`proposalStatus.${scope.row.status}`) }}
        </el-tag>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.expiry')" width="104">
      <template #default="scope">
        <span :class="{ 'proposal-queue__expired': isPastDeadline(scope.row.expiresAtUtc) }">
          {{ formatTime(scope.row.expiresAtUtc) }}
        </span>
      </template>
    </el-table-column>

    <el-table-column :label="t('market.commands')" width="260" fixed="right">
      <template #default="scope">
        <div class="inline-actions">
          <el-button link @click="requestSelect(scope.row.proposalId)">
            {{ t('market.viewDetails') }}
          </el-button>
          <el-button
            link
            type="success"
            :disabled="!scope.row.isDecidable || isSaving"
            @click="requestDecision('approve', scope.row.proposalId)"
          >
            {{ t('market.approve') }}
          </el-button>
          <el-button
            link
            :disabled="!scope.row.isDecidable || isSaving"
            @click="requestDecision('suspend', scope.row.proposalId)"
          >
            {{ t('market.suspend') }}
          </el-button>
          <el-button
            link
            type="danger"
            :disabled="!scope.row.isDecidable || isSaving"
            @click="requestDecision('reject', scope.row.proposalId)"
          >
            {{ t('market.reject') }}
          </el-button>
        </div>
      </template>
    </el-table-column>
  </el-table>
</template>

<style scoped lang="less" src="./ProposalQueue.less"></style>
