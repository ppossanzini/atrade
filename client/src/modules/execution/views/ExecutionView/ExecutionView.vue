<script lang="ts" src="./ExecutionView.ts"></script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-header__title">{{ t('execution.title') }}</h1>
        <p class="page-header__subtitle">{{ t('execution.subtitle') }}</p>
      </div>

      <div class="inline-actions">
        <el-tag v-if="executionStore.compensationCount > 0" type="danger" effect="dark">
          {{ t('execution.metrics.compensation') }}: {{ executionStore.compensationCount }}
        </el-tag>
        <el-button :loading="executionStore.isLoading" @click="refresh">
          {{ t('execution.refresh') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="executionStore.hasLoadFailure"
      class="execution-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <div class="execution-metrics">
      <div class="panel execution-metric">
        <span class="panel-title">{{ t('execution.metrics.open') }}</span>
        <strong class="execution-metric__value">{{ executionStore.openCount }}</strong>
        <small>{{ t('execution.metrics.openHint') }}</small>
      </div>
      <div class="panel execution-metric">
        <span class="panel-title">{{ t('execution.metrics.nominal') }}</span>
        <strong class="execution-metric__value">{{ executionStore.nominalCount }}</strong>
        <small>{{ t('execution.metrics.nominalHint') }}</small>
      </div>
      <div class="panel execution-metric">
        <span class="panel-title">{{ t('execution.metrics.partial') }}</span>
        <strong class="execution-metric__value">{{ executionStore.partialCount }}</strong>
        <small>{{ t('execution.metrics.partialHint') }}</small>
      </div>
      <div class="panel execution-metric">
        <span class="panel-title">{{ t('execution.metrics.compensation') }}</span>
        <strong class="execution-metric__value">{{ executionStore.compensationCount }}</strong>
        <small>{{ t('execution.metrics.compensationHint') }}</small>
      </div>
    </div>

    <div class="panel execution-panel">
      <div class="panel-header">
        <span class="panel-title">{{ t('execution.queueTitle') }}</span>
        <span class="execution-queue__subtitle">{{ t('execution.queueSubtitle') }}</span>
      </div>

      <el-table
        v-loading="executionStore.isLoading"
        :data="executionStore.queue"
        size="small"
        empty-text=" "
      >
        <el-table-column :label="t('execution.columns.created')" min-width="150">
          <template #default="scope">{{ formatTimestamp(scope.row.createdAtUtc) }}</template>
        </el-table-column>
        <el-table-column :label="t('execution.columns.basket')" min-width="150">
          <template #default="scope">
            {{ scope.row.basketName }} · v{{ scope.row.versionNumber }}
          </template>
        </el-table-column>
        <el-table-column :label="t('execution.columns.status')" min-width="150">
          <template #default="scope">
            <el-tag :type="statusTag(scope.row.status)" effect="plain" size="small">
              {{ statusLabel(scope.row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('execution.columns.coverage')" width="190">
          <template #default="scope">
            <div class="execution-coverage">
              <el-progress
                :percentage="scope.row.coverage"
                :stroke-width="8"
                :status="scope.row.coverage === 100 ? 'success' : undefined"
              />
              <small>
                {{
                  t('execution.coverageOf', {
                    filled: scope.row.filledLegCount,
                    total: scope.row.legCount,
                  })
                }}
              </small>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('execution.columns.policy')" min-width="150">
          <template #default="scope">{{ policyLabel(scope.row.failurePolicy) }}</template>
        </el-table-column>
        <el-table-column width="110" align="right">
          <template #default="scope">
            <el-button link type="primary" @click="openDetail(scope.row.executionId)">
              {{ t('execution.columns.detail') }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <p v-if="!executionStore.isLoading && executionStore.queue.length === 0" class="panel-empty">
        {{ t('execution.empty') }}
      </p>
    </div>

    <el-drawer
      :model-value="executionStore.detail !== null"
      :title="t('execution.detailTitle')"
      size="min(48rem, 96vw)"
      @close="executionStore.closeDetail"
    >
      <div v-if="executionStore.detail" class="execution-detail">
        <div class="execution-detail__heading">
          <div>
            <span class="panel-title">
              {{ formatIdentifier(executionStore.detail.executionId) }}
            </span>
            <strong>
              {{ executionStore.detail.basketName }} · v{{ executionStore.detail.versionNumber }}
            </strong>
          </div>
          <el-tag :type="statusTag(executionStore.detail.status)" effect="dark">
            {{ statusLabel(executionStore.detail.status) }}
          </el-tag>
        </div>

        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.policy') }}</span>
          <span class="metric-row__value">
            {{ policyLabel(executionStore.detail.failurePolicy) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.minimumCoverage') }}</span>
          <span class="metric-row__value">{{ executionStore.detail.minimumCoverage }} %</span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.coverage') }}</span>
          <span class="metric-row__value">{{ executionStore.detail.coverage }} %</span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.started') }}</span>
          <span class="metric-row__value">
            {{ formatTimestamp(executionStore.detail.startedAtUtc) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.completed') }}</span>
          <span class="metric-row__value">
            {{ formatTimestamp(executionStore.detail.completedAtUtc) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.proposal') }}</span>
          <span class="metric-row__value">
            {{
              executionStore.detail.proposalId
                ? formatIdentifier(executionStore.detail.proposalId)
                : t('execution.detailFields.noProposal')
            }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('execution.detailFields.compensationOf') }}</span>
          <span class="metric-row__value">
            {{ formatIdentifier(executionStore.detail.compensationOfExecutionId) }}
          </span>
        </div>

        <span class="panel-title panel-title--spaced">{{ t('execution.legs') }}</span>
        <el-table :data="executionStore.detail.legs" size="small">
          <el-table-column :label="t('execution.legsColumns.ordinal')" prop="ordinal" width="48" />
          <el-table-column
            :label="t('execution.legsColumns.symbol')"
            prop="symbol"
            min-width="90"
          />
          <el-table-column :label="t('execution.legsColumns.direction')" width="86">
            <template #default="scope">{{ directionLabel(scope.row.direction) }}</template>
          </el-table-column>
          <el-table-column
            :label="t('execution.legsColumns.planned')"
            prop="volumeUnits"
            width="104"
            align="right"
          />
          <el-table-column
            :label="t('execution.legsColumns.filled')"
            prop="filledVolumeUnits"
            width="104"
            align="right"
          />
          <el-table-column :label="t('execution.legsColumns.status')" width="150">
            <template #default="scope">
              <el-tag :type="legTag(scope.row.status)" effect="plain" size="small">
                {{ legStatusLabel(scope.row.status) }}
              </el-tag>
            </template>
          </el-table-column>
        </el-table>

        <div class="execution-detail__identifiers">
          <div v-for="leg in executionStore.detail.legs" :key="leg.legId" class="metric-row">
            <span class="metric-row__label">
              {{ leg.symbol }} · {{ t('execution.legsColumns.clientOrderId') }}
            </span>
            <span class="metric-row__value execution-detail__identifier">
              {{ leg.clientOrderId || t('risk.notAvailable') }}
            </span>
          </div>
          <div
            v-for="leg in executionStore.detail.legs"
            :key="`${leg.legId}-broker`"
            class="metric-row"
          >
            <span class="metric-row__label">
              {{ leg.symbol }} · {{ t('execution.legsColumns.brokerOrderId') }}
            </span>
            <span class="metric-row__value execution-detail__identifier">
              {{ leg.brokerOrderId || t('risk.notAvailable') }}
            </span>
          </div>
          <div
            v-for="leg in executionStore.detail.legs.filter((item) => item.errorCode)"
            :key="`${leg.legId}-error`"
            class="metric-row"
          >
            <span class="metric-row__label">
              {{ leg.symbol }} · {{ t('execution.legsColumns.errorCode') }}
            </span>
            <span class="metric-row__value execution-detail__error">{{ leg.errorCode }}</span>
          </div>
        </div>

        <span class="panel-title panel-title--spaced">{{ t('execution.events') }}</span>
        <el-table
          :data="executionStore.detail.events"
          size="small"
          :empty-text="t('execution.noEvents')"
        >
          <el-table-column :label="t('execution.eventsColumns.receivedAt')" min-width="150">
            <template #default="scope">{{ formatTimestamp(scope.row.receivedAtUtc) }}</template>
          </el-table-column>
          <el-table-column :label="t('execution.eventsColumns.kind')" min-width="150">
            <template #default="scope">{{ eventKindLabel(scope.row.kind) }}</template>
          </el-table-column>
          <el-table-column :label="t('execution.eventsColumns.symbol')" prop="symbol" width="96" />
          <el-table-column
            :label="t('execution.eventsColumns.brokerEventId')"
            prop="brokerEventId"
            min-width="200"
            show-overflow-tooltip
          />
        </el-table>

        <div class="inline-actions execution-detail__actions">
          <el-button :loading="executionStore.isLoading" @click="refresh">
            {{ t('execution.refresh') }}
          </el-button>
          <el-button
            type="danger"
            :disabled="!executionStore.detail.needsCompensation || executionStore.isSaving"
            @click="openCompensation"
          >
            {{ t('execution.compensate') }}
          </el-button>
        </div>
      </div>
    </el-drawer>

    <el-dialog
      :model-value="isCompensationDialogOpen"
      :title="t('execution.compensateTitle')"
      width="min(32rem, 92vw)"
      @close="closeCompensation"
    >
      <el-alert
        class="execution-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t('execution.compensateBody')"
      />

      <el-form label-position="top">
        <el-form-item :label="t('execution.compensateReason')">
          <el-input
            v-model="compensationReason"
            type="textarea"
            :rows="3"
            maxlength="256"
            show-word-limit
            :placeholder="t('execution.compensateReasonPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="closeCompensation">{{ t('common.cancel') }}</el-button>
        <el-button
          type="primary"
          :loading="executionStore.isSaving"
          :disabled="compensationDisabled"
          @click="confirmCompensation"
        >
          {{ t('common.confirm') }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped lang="less" src="./ExecutionView.less"></style>
